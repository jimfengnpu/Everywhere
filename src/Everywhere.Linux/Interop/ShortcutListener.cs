using Avalonia.Input;
using Everywhere.Interop;
using System.Reactive.Disposables;

namespace Everywhere.Linux.Interop;

public class ShortcutListener(IEventHelper eventHelper) : IShortcutListener
{
    // Register a keyboard hotkey. Multiple handlers for the same hotkey are supported.
    // Returns an IDisposable that unregisters this handler only.
    public IDisposable Register(KeyboardShortcut hotkey, Action handler)
    {
        if (hotkey.Key == Key.None || hotkey.Modifiers == KeyModifiers.None)
            throw new ArgumentException("Invalid keyboard hotkey.", nameof(hotkey));
        ArgumentNullException.ThrowIfNull(handler);
        var id = eventHelper.GrabKey(hotkey, handler);
        return id != 0 ?
            Disposable.Create(() => eventHelper.UngrabKey(id)) :
            throw new InvalidOperationException("Failed to grab the hotkey. The key combination may be already in use.");
    }

    // Register a mouse hotkey. Multiple handlers for the same MouseKey (with different delays) are supported.
    // Returns an IDisposable that unregisters this handler only.
    public IDisposable Register(MouseShortcut hotkey, Action handler)
    {
        if (hotkey.Key == MouseButton.None)
            throw new ArgumentException("Invalid mouse hotkey.", nameof(hotkey));
        ArgumentNullException.ThrowIfNull(handler);
        var id = eventHelper.GrabMouse(hotkey, handler);
        return id != 0 ?
            Disposable.Create(() => eventHelper.UngrabMouse(id)) :
            throw new InvalidOperationException("Failed to grab the hotkey. The key combination may be already in use.");
    }

    /// <summary>
    /// Starts capturing the keyboard hotkey
    /// </summary>
    public IKeyboardShortcutScope StartCaptureKeyboardShortcut()
    {
        return new KeyboardShortcutScopeImpl(eventHelper);
    }

    private class KeyboardShortcutScopeImpl : IKeyboardShortcutScope
    {
        public KeyboardShortcut PressingShortcut { get; private set; }

        public bool IsDisposed { get; private set; }

        public event IKeyboardShortcutScope.PressingShortcutChangedHandler? PressingShortcutChanged;

        public event IKeyboardShortcutScope.ShortcutFinishedHandler? ShortcutFinished;

        private KeyModifiers _pressedKeyModifiers = KeyModifiers.None;
        private readonly IEventHelper _eventHelper;

        public KeyboardShortcutScopeImpl(IEventHelper eventHelper)
        {
            IsDisposed = false;
            _eventHelper = eventHelper;
            _eventHelper.GrabKeyHook((hotkey, eventType) =>
            {
                if (eventType == EventType.KeyDown)
                {
                    if (hotkey.Modifiers != KeyModifiers.None)
                    {
                        _pressedKeyModifiers |= hotkey.Modifiers;
                        PressingShortcut = PressingShortcut with { Modifiers = _pressedKeyModifiers };
                    }
                    PressingShortcut = PressingShortcut with { Key = hotkey.Key };
                    PressingShortcutChanged?.Invoke(this, PressingShortcut);
                }
                else
                {
                    _pressedKeyModifiers &= ~hotkey.Modifiers;
                    if (_pressedKeyModifiers == KeyModifiers.None)
                    {
                        if (PressingShortcut.Modifiers != KeyModifiers.None && PressingShortcut.Key == Key.None)
                        {
                            PressingShortcut = default; // modifiers only hotkey, reset it
                        }

                        // system key is all released, capture is done
                        PressingShortcutChanged?.Invoke(this, PressingShortcut);
                        ShortcutFinished?.Invoke(this, PressingShortcut);
                    }
                }
            });
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            _eventHelper.UngrabKeyHook();
            IsDisposed = true;
        }
    }
}