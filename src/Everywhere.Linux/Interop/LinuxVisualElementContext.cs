using Avalonia;
using Everywhere.Interop;

namespace Everywhere.Linux.Interop;

public class LinuxVisualElementContext : IVisualElementContext
{
    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;

    public IVisualElement? KeyboardFocusedElement => null;

    public IVisualElement? ElementFromPoint(PixelPoint point, PickElementMode mode = PickElementMode.Element)
    {
        return null;
    }

    public IVisualElement? ElementFromPointer(PickElementMode mode = PickElementMode.Element)
    {
        return null;
    }

    public Task<IVisualElement?> PickElementAsync(PickElementMode mode)
    {
        return Task.FromResult<IVisualElement?>(null);
    }
}
