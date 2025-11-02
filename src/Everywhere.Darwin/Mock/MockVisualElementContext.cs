using Avalonia;
using Everywhere.Interop;

namespace Everywhere.Darwin.Mock;

public class MockVisualElementContext : IVisualElementContext
{
    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;
    public IVisualElement? KeyboardFocusedElement { get; set; }
    public IVisualElement? ElementFromPoint(PixelPoint point, PickElementMode mode = PickElementMode.Element)
    {
        return null;
    }

    public IVisualElement? ElementFromPointer(PickElementMode mode = PickElementMode.Element)
    {
        return null;
    }

    public IVisualElement? PointerOverElement { get; set; }

    public IVisualElement? ElementFromPoint(PixelPoint point) => null;

    public Task<IVisualElement?> PickElementAsync(PickElementMode mode) => Task.FromResult<IVisualElement?>(null);
}