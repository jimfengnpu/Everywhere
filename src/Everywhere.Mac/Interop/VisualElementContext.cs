using Avalonia;
using Avalonia.Threading;
using Everywhere.Interop;
using ShadUI.Extensions;
using ZLinq;

namespace Everywhere.Mac.Interop;

public partial class VisualElementContext(IWindowHelper windowHelper) : IVisualElementContext
{
    public IVisualElement? KeyboardFocusedElement => AXUIElement.SystemWide.ElementByAttributeValue(AXAttributeConstants.FocusedUIElement);

    IVisualElement? IVisualElementContext.ElementFromPoint(PixelPoint point, ElementPickMode mode) => ElementFromPoint(point, mode);

    private static IVisualElement? ElementFromPoint(PixelPoint point, ElementPickMode mode = ElementPickMode.Element)
    {
        switch (mode)
        {
            case ElementPickMode.Element:
            {
                return AXUIElement.SystemWide.ElementAtPosition(point.X, point.Y);
            }
            case ElementPickMode.Window:
            {
                // Traverse up to find the containing window element
                IVisualElement? current = AXUIElement.SystemWide.ElementAtPosition(point.X, point.Y);
                while (current is AXUIElement axui && axui.Role != AXRoleAttribute.AXWindow)
                {
                    current = current.Parent;
                }
                return current;
            }
            case ElementPickMode.Screen:
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

    public IVisualElement? ElementFromPointer(ElementPickMode mode = ElementPickMode.Element)
    {
        var point = Dispatcher.UIThread.InvokeOnDemand<PixelPoint?>(() =>
        {
            // NSEvent.CurrentMouseLocation gives coordinates with the origin at the bottom-left of the primary screen.
            var mouseLocation = NSEvent.CurrentMouseLocation;

            // We need to find which screen the mouse is on to correctly convert coordinates.
            var screen = NSScreen.Screens.AsValueEnumerable().FirstOrDefault(s => s.Frame.Contains(mouseLocation)) ?? NSScreen.MainScreen;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (screen is null) return null;

            // Convert to a top-left origin coordinate system.
            var y = screen.Frame.Height - (mouseLocation.Y - screen.Frame.Y);
            var x = mouseLocation.X - screen.Frame.X;
            return new PixelPoint((int)x, (int)y);
        });

        return point is null ? null : ElementFromPoint(point.Value, mode);
    }

    public Task<IVisualElement?> PickElementAsync(ElementPickMode mode)
    {
        return VisualElementPicker.PickAsync(windowHelper, mode);
    }
}