using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Utilities;

namespace Everywhere.Darwin.Mock;

public class MockShortcutListener : IShortcutListener
{

    public IDisposable Register(KeyboardShortcut shortcut, Action handler)
    {
        return new AnonymousDisposable(() => { });
    }

    public IDisposable Register(MouseShortcut shortcut, Action handler)
    {
        return new AnonymousDisposable(() => { });
    }

    public IKeyboardShortcutScope StartCaptureKeyboardShortcut()
    {
        throw new NotImplementedException();
    }
}