using System.Reactive.Disposables;
using Avalonia.Input;
using CoreFoundation;
using Everywhere.Interop;
using Everywhere.Utilities;
using Microsoft.Extensions.Logging;

namespace Everywhere.Mac.Interop;

/// <summary>
/// Provides a global keyboard and mouse shortcut listener for macOS using CoreGraphics Event Taps.
/// Requires Accessibility permissions.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class CGEventShortcutListener : IShortcutListener, IDisposable
{
    private readonly AutoResetEvent _readySignal = new(false);
    private readonly Dictionary<KeyboardShortcut, List<Action>> _keyboardHandlers = new();
    private readonly Dictionary<MouseShortcut, List<Action>> _mouseHandlers = new();
    private readonly Lock _syncLock = new();
    private readonly ILogger<CGEventShortcutListener> _logger;

    private CFMachPort? _eventTap;
    private CFRunLoopSource? _runLoopSource;
    private KeyboardShortcutScopeImpl? _currentCaptureScope;

    public CGEventShortcutListener(ILogger<CGEventShortcutListener> logger)
    {
        _logger = logger;

        // It's crucial to check for permissions before attempting to create the tap.
        if (!PermissionHelper.IsAccessibilityTrusted())
        {
            logger.LogError("Accessibility permissions are not granted. Cannot create global shortcut listener.");
            return;
        }

        var workerThread = new Thread(RunLoopThread)
        {
            Name = "CGEventShortcutListenerThread",
            IsBackground = true
        };
        workerThread.Start();
        _readySignal.WaitOne();
    }

    private void RunLoopThread()
    {
        using var pool = new NSAutoreleasePool();
        using var tap = CreateTap();
        _eventTap = tap;
        _runLoopSource = tap.CreateRunLoopSource();
        CFRunLoop.Current.AddSource(_runLoopSource, CFRunLoop.ModeDefault);
        _readySignal.Set();
        CFRunLoop.Current.Run();
    }

    private CFMachPort CreateTap()
    {
        const CGEventMask mask =
            CGEventMask.KeyDown | CGEventMask.KeyUp | CGEventMask.FlagsChanged |
            CGEventMask.LeftMouseDown | CGEventMask.LeftMouseUp |
            CGEventMask.RightMouseDown | CGEventMask.RightMouseUp |
            CGEventMask.OtherMouseDown | CGEventMask.OtherMouseUp;

        var tap = CGEvent.CreateTap(
            CGEventTapLocation.HID,
            CGEventTapPlacement.HeadInsert,
            CGEventTapOptions.Default,
            mask,
            HandleEvent,
            IntPtr.Zero);

        return tap ?? throw new InvalidOperationException("CGEvent tap creation failed.");
    }

    private nint HandleEvent(nint proxy, CGEventType type, nint cgEventRef, nint userData)
    {
        // Early exit if the event tap is not initialized
        if (_eventTap is null) return cgEventRef;

        if (type is CGEventType.TapDisabledByTimeout or CGEventType.TapDisabledByUserInput)
        {
            CGEvent.TapEnable(_eventTap);
            return cgEventRef;
        }

        var cgEvent = CoreFoundationInterop.CGEventFromHandle(cgEventRef);
        switch (type)
        {
            case CGEventType.KeyDown:
                HandleKeyDown(cgEvent, ref cgEventRef);
                break;
            case CGEventType.KeyUp:
                HandleKeyUp(ref cgEventRef);
                break;
            case CGEventType.FlagsChanged:
                HandleFlagsChanged(cgEvent, ref cgEventRef);
                break;
            case CGEventType.LeftMouseDown:
            case CGEventType.LeftMouseUp:
            case CGEventType.RightMouseDown:
            case CGEventType.RightMouseUp:
            case CGEventType.OtherMouseDown:
            case CGEventType.OtherMouseUp:
                // HandleMouse(type, nsEvent);
                break;
        }

        return cgEventRef;
    }

    private bool _needToSwallowModifierKey;

    private void HandleKeyDown(CGEvent cgEvent, ref nint cgEventRef)
    {
        var key = ((ushort)cgEvent.GetLongValueField(CGEventField.KeyboardEventKeycode)).ToAvaloniaKey();
        var modifiers = cgEvent.Flags.ToAvaloniaKeyModifiers();
        var shortcut = new KeyboardShortcut(key, modifiers);

        List<Action>? handlers = null;
        using (var _ = _syncLock.EnterScope())
        {
            if (_currentCaptureScope != null)
            {
                // If we are in capture mode, update the current shortcut being pressed.
                _currentCaptureScope.PressingShortcut = _currentCaptureScope.PressingShortcut with { Key = shortcut.Key };
                cgEventRef = 0; // Swallow the event
                return;
            }

            if (_keyboardHandlers.TryGetValue(shortcut, out var registeredHandlers))
            {
                handlers = [.. registeredHandlers];
            }
        }

        if (handlers != null)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    // Swallow exceptions from handlers to avoid crashing the event loop.
                    _logger.LogError(ex, "Exception occurred while handling keyboard shortcut {Shortcut}.", shortcut);
                }
            }

            cgEventRef = 0; // Swallow the event

            // If a shortcut was handled, we may need to swallow the following modifier key event.
            if (modifiers != KeyModifiers.None) _needToSwallowModifierKey = true;
        }
    }

    private void HandleKeyUp(ref nint cgEventRef)
    {
        // If we are in capture mode, notify that the shortcut has been finished.
        using var _ = _syncLock.EnterScope();
        if (_currentCaptureScope is null) return;

        _currentCaptureScope.NotifyShortcutFinished();
        cgEventRef = 0; // Swallow the event
    }

    private void HandleFlagsChanged(CGEvent cgEvent, ref nint cgEventRef)
    {
        var modifiers = cgEvent.Flags.ToAvaloniaKeyModifiers();
        if (_needToSwallowModifierKey)
        {
            // Swallow the following modifier key event until no modifiers are pressed.
            cgEventRef = 0;
            if (modifiers == KeyModifiers.None) _needToSwallowModifierKey = false;
        }

        using var _ = _syncLock.EnterScope();
        if (_currentCaptureScope is null) return;

        _currentCaptureScope.PressingShortcut = _currentCaptureScope.PressingShortcut with { Modifiers = modifiers };
        cgEventRef = 0; // Swallow the event
    }

    public IDisposable Register(KeyboardShortcut shortcut, Action handler)
    {
        if (shortcut.Key == Key.None || shortcut.Modifiers == KeyModifiers.None)
            throw new ArgumentException("Invalid keyboard shortcut.", nameof(shortcut));
        ArgumentNullException.ThrowIfNull(handler);

        using var _ = _syncLock.EnterScope();
        if (!_keyboardHandlers.TryGetValue(shortcut, out var handlers))
        {
            handlers = [];
            _keyboardHandlers[shortcut] = handlers;
        }

        handlers.Add(handler);
        return Disposable.Create(() =>
        {
            using var _ = _syncLock.EnterScope();
            if (_keyboardHandlers.TryGetValue(shortcut, out var existingHandlers))
            {
                existingHandlers.Remove(handler);
                if (existingHandlers.Count == 0)
                {
                    _keyboardHandlers.Remove(shortcut);
                }
            }
        });
    }

    public IDisposable Register(MouseShortcut shortcut, Action handler)
    {
        // TODO: Implement mouse shortcut registration.
        // This will involve listening to mouse events in HandleEvent,
        // managing timers for delays, and invoking handlers.
        throw new NotImplementedException();
    }

    public IKeyboardShortcutScope StartCaptureKeyboardShortcut()
    {
        using var _ = _syncLock.EnterScope();
        if (_currentCaptureScope != null) return _currentCaptureScope;

        // Start a new capture scope
        var scope = new KeyboardShortcutScopeImpl(this);
        _currentCaptureScope = scope;
        return scope;
    }

    public void Dispose()
    {
        if (_runLoopSource != null)
        {
            CFRunLoop.Current.RemoveSource(_runLoopSource, CFRunLoop.ModeDefault);
            _runLoopSource.Dispose();
            _runLoopSource = null;
        }

        DisposeCollector.DisposeToDefault(ref _eventTap);
    }

    /// <summary>
    /// Implementation of IKeyboardShortcutScope for capturing keyboard shortcuts.
    /// This class is intended to be used internally by CGEventShortcutListener.
    /// </summary>
    private sealed class KeyboardShortcutScopeImpl(CGEventShortcutListener owner) : IKeyboardShortcutScope
    {
        public KeyboardShortcut PressingShortcut
        {
            get;
            set
            {
                if (field == value) return;
                field = value;
                PressingShortcutChanged?.Invoke(this, value);
            }
        }

        public bool IsDisposed { get; private set; }

        public event IKeyboardShortcutScope.PressingShortcutChangedHandler? PressingShortcutChanged;

        public event IKeyboardShortcutScope.ShortcutFinishedHandler? ShortcutFinished;

        public void NotifyShortcutFinished() => ThreadPool.QueueUserWorkItem(_ => ShortcutFinished?.Invoke(this, PressingShortcut));

        public void Dispose()
        {
            if (IsDisposed) return;

            using var _ = owner._syncLock.EnterScope();
            if (owner._currentCaptureScope == this) owner._currentCaptureScope = null;
            IsDisposed = true;
        }
    }
}