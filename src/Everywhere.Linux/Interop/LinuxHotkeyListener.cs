using Everywhere.Interop;

namespace Everywhere.Linux.Interop;

public class LinuxHotkeyListener : IHotkeyListener
{
    public IDisposable Register(KeyboardHotkey hotkey, Action handler)
    {
        // Minimal stub: not supporting global hotkeys on Linux in this implementation.
        return new DummyDisposable();
    }

    public IDisposable Register(MouseHotkey hotkey, Action handler)
    {
        return new DummyDisposable();
    }

    public IKeyboardHotkeyScope StartCaptureKeyboardHotkey()
    {
        throw new NotSupportedException("Keyboard hotkey capture is not supported on this platform implementation.");
    }

    private sealed class DummyDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
