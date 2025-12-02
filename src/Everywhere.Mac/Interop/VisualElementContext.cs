using Avalonia;
using Avalonia.Threading;
using Everywhere.Interop;
using ShadUI.Extensions;

namespace Everywhere.Mac.Interop;

public partial class VisualElementContext(IWindowHelper windowHelper) : IVisualElementContext
{
    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;

    public IVisualElement? KeyboardFocusedElement => AXUIElement.SystemWide.ElementByAttributeValue(AXAttributeConstants.FocusedUIElement);

    public IVisualElement? PointerOverElement
    {
        get
        {
            var mouseLocation = NSEvent.CurrentMouseLocation;
            var primaryScreen = NSScreen.MainScreen;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (primaryScreen is null) return null;

            // Convert from screen coordinates (bottom-left origin) to accessibility coordinates (top-left origin)
            var point = new CGPoint(mouseLocation.X, primaryScreen.Frame.Height - mouseLocation.Y);
            return ElementFromPoint(new PixelPoint((int)point.X, (int)point.Y));
        }
    }

    IVisualElement? IVisualElementContext.ElementFromPoint(PixelPoint point, PickElementMode mode) => ElementFromPoint(point, mode);

    private static IVisualElement? ElementFromPoint(PixelPoint point, PickElementMode mode = PickElementMode.Element)
    {
        switch (mode)
        {
            case PickElementMode.Element:
            {
                return AXUIElement.SystemWide.ElementAtPosition(point.X, point.Y);
            }
            case PickElementMode.Window:
            {
                // Traverse up to find the containing window element
                IVisualElement? current = AXUIElement.SystemWide.ElementAtPosition(point.X, point.Y);
                while (current is AXUIElement axui && axui.Role != AXRoleAttribute.AXWindow)
                {
                    current = current.Parent;
                }
                return current;
            }
            case PickElementMode.Screen:
            {
                var screen = NSScreen.Screens.FirstOrDefault(s => s.Frame.Contains(new CGPoint(point.X, point.Y)));
                return screen is null ? null : new NSScreenVisualElement(screen);
            }
            default:
            {
                return null;
            }
        }
    }

    public IVisualElement? ElementFromPointer(PickElementMode mode = PickElementMode.Element)
    {
        var point = Dispatcher.UIThread.InvokeOnDemand<PixelPoint?>(() =>
        {
            // NSEvent.CurrentMouseLocation gives coordinates with the origin at the bottom-left of the primary screen.
            var mouseLocation = NSEvent.CurrentMouseLocation;

            // We need to find which screen the mouse is on to correctly convert coordinates.
            var screen = NSScreen.Screens.FirstOrDefault(s => s.Frame.Contains(mouseLocation)) ?? NSScreen.MainScreen;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (screen is null) return null;

            // Convert to a top-left origin coordinate system.
            var y = screen.Frame.Height - (mouseLocation.Y - screen.Frame.Y);
            var x = mouseLocation.X - screen.Frame.X;
            return new PixelPoint((int)x, (int)y);
        });

        return point is null ? null : ElementFromPoint(point.Value, mode);
    }

    public Task<IVisualElement?> PickElementAsync(PickElementMode mode)
    {
        return VisualElementPicker.PickAsync(windowHelper, mode);
    }

    // TODO: Implement AXObserver to raise KeyboardFocusedElementChanged event.
}