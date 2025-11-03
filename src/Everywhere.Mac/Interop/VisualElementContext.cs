using Avalonia;
using Avalonia.Threading;
using Everywhere.Interop;
using ShadUI.Extensions;

namespace Everywhere.Mac.Interop;

public class VisualElementContext : IVisualElementContext
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

    public IVisualElement? ElementFromPoint(PixelPoint point, PickElementMode mode = PickElementMode.Element)
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
                return screen is null ? null : new ScreenVisualElementImpl(this, screen);
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
        // The implementation of ElementPickerWindow on macOS would be analogous to the Windows version.
        // It would involve creating a full-screen borderless window, taking a screenshot of all screens,
        // and then using mouse events to select an element.
        // This is a complex UI task that requires its own class.
        // For now, we can throw NotImplementedException.
        throw new NotImplementedException("ElementPickerWindow for macOS needs to be implemented.");
    }

    // TODO: Implement AXObserver to raise KeyboardFocusedElementChanged event.
}

// A placeholder for the Screen implementation on macOS
file class ScreenVisualElementImpl : IVisualElement
{
    public ScreenVisualElementImpl(IVisualElementContext context, NSScreen screen)
    { /* ... */
    }

    // ... implementation would be similar to the Windows version, but using NSScreen and AppKit APIs
    // to find windows on this screen.
    public IVisualElementContext Context => throw new NotImplementedException();
    public string Id => throw new NotImplementedException();
    public IVisualElement? Parent => throw new NotImplementedException();
    public IEnumerable<IVisualElement> Children => throw new NotImplementedException();
    public IVisualElement? PreviousSibling => throw new NotImplementedException();
    public IVisualElement? NextSibling => throw new NotImplementedException();
    public VisualElementType Type => VisualElementType.Screen;
    public VisualElementStates States => VisualElementStates.None;
    public string? Name => null;
    public PixelRect BoundingRectangle => throw new NotImplementedException();
    public int ProcessId => 0;
    public nint NativeWindowHandle => 0;
    public string? GetText(int maxLength = -1) => null;
    public void Invoke() { }
    public void SetText(string text) { }
    public void SendShortcut(KeyboardShortcut shortcut) { }
    public string? GetSelectionText() => null;
    public Task<Avalonia.Media.Imaging.Bitmap> CaptureAsync() => throw new NotImplementedException();
}