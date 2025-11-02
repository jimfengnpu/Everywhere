using Avalonia.Controls;
using Everywhere.Interop;

namespace Everywhere.Darwin.Mock;

public class MockWindowHelper : IWindowHelper
{
    public void SetFocusable(Window window, bool focusable)
    {
    }

    public void SetHitTestVisible(Window window, bool visible)
    {
    }

    public bool GetEffectiveVisible(Window window)
    {
        return false;
    }

    public void SetCloaked(Window window, bool cloaked)
    {
    }

    public bool AnyModelDialogOpened(Window window)
    {
        return false;
    }
}