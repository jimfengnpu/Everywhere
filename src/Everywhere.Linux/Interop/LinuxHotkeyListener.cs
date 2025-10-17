using Avalonia.Input;
using Everywhere.Common;
using Everywhere.Interop;
using Everywhere.Utilities;

namespace Everywhere.Linux.Interop;

public class LinuxHotkeyListener : IHotkeyListener
{
    private readonly ILinuxDisplayBackend? _backend = ServiceLocator.Resolve<ILinuxDisplayBackend>();

    // Register a keyboard hotkey. Multiple handlers for the same hotkey are supported.
    // Returns an IDisposable that unregisters this handler only.
    public IDisposable Register(KeyboardHotkey hotkey, Action handler)
    {
        if (hotkey.Key == Key.None || hotkey.Modifiers == KeyModifiers.None)
            throw new ArgumentException("Invalid keyboard hotkey.", nameof(hotkey));
        ArgumentNullException.ThrowIfNull(handler);
        var id = _backend?.GrabKey(hotkey, handler)??0;
        return id != 0
            ? new AnonymousDisposable(() => _backend?.UngrabKey(id))
            : throw new InvalidOperationException("Failed to grab the hotkey. The key combination may be already in use.");
    }

    // Register a mouse hotkey. Multiple handlers for the same MouseKey (with different delays) are supported.
    // Returns an IDisposable that unregisters this handler only.
    public IDisposable Register(MouseHotkey hotkey, Action handler)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Starts capturing the keyboard hotkey
    /// </summary>
    /// <returns></returns>
    public IKeyboardHotkeyScope StartCaptureKeyboardHotkey()
    {
        return new LinuxKeyboardHotkeyScopeImpl(_backend ?? throw new InvalidOperationException("Display backend is not available."));
    }
}

public class LinuxKeyboardHotkeyScopeImpl : IKeyboardHotkeyScope
{
    private readonly ILinuxDisplayBackend _backend;
    public KeyboardHotkey PressingHotkey { get; private set; }

    public bool IsDisposed { get; private set; }

    public event IKeyboardHotkeyScope.PressingHotkeyChangedHandler? PressingHotkeyChanged;

    public event IKeyboardHotkeyScope.HotkeyFinishedHandler? HotkeyFinished;
    
    private KeyModifiers _pressedKeyModifiers = KeyModifiers.None;
    
    public LinuxKeyboardHotkeyScopeImpl(ILinuxDisplayBackend backend)
    {
        _backend = backend;
        IsDisposed = false;
        _backend.GrabAll((hotkey, keydown) =>
        {
            if (keydown)
            {
                if (hotkey.Modifiers != KeyModifiers.None)
                {
                    _pressedKeyModifiers |= hotkey.Modifiers;
                    PressingHotkey = PressingHotkey with { Modifiers = _pressedKeyModifiers };
                }
                PressingHotkey = PressingHotkey with { Key = hotkey.Key };
                PressingHotkeyChanged?.Invoke(this, PressingHotkey);
            }
            else
            {
                _pressedKeyModifiers &= ~hotkey.Modifiers;
                if (_pressedKeyModifiers == KeyModifiers.None)
                {
                    if (PressingHotkey.Modifiers != KeyModifiers.None && PressingHotkey.Key == Key.None)
                    {
                        PressingHotkey = default; // modifiers only hotkey, reset it
                    }

                    // system key is all released, capture is done
                    PressingHotkeyChanged?.Invoke(this, PressingHotkey);
                    HotkeyFinished?.Invoke(this, PressingHotkey);
                }
            }
        });
    }
    
    public void Dispose()
    {
        if (IsDisposed) return;
        _backend.UngrabAll();
        IsDisposed = true;
    }
}