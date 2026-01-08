using System.Runtime.InteropServices;
using Avalonia;
using Everywhere.Interop;
using ObjCRuntime;

namespace Everywhere.Mac.Interop;

public partial class VisualElementContext
{
    private class VisualElementPicker : ScreenSelectionSession
    {
        public static Task<IVisualElement?> PickAsync(IWindowHelper windowHelper, ScreenSelectionMode mode)
        {
            var window = new VisualElementPicker(windowHelper, mode);
            window.Show();
            return window._pickingPromise.Task;
        }

        private readonly TaskCompletionSource<IVisualElement?> _pickingPromise = new();

        /// <summary>
        /// We reuse a single list to avoid allocations during picking.
        /// </summary>
        private readonly List<int> _windowOwnerPids = [];

        private IVisualElement? _selectedElement;

        private VisualElementPicker(IWindowHelper windowHelper, ScreenSelectionMode screenSelectionMode)
            : base(
                windowHelper,
                [ScreenSelectionMode.Screen, ScreenSelectionMode.Window, ScreenSelectionMode.Element],
                screenSelectionMode)
        {
        }

        protected override void OnCanceled()
        {
            _selectedElement = null;
        }

        protected override void OnCloseCleanup()
        {
            _pickingPromise.TrySetResult(_selectedElement);
        }

        protected override void OnMove(CGPoint point)
        {
            var maskRect = new PixelRect();
            switch (CurrentMode)
            {
                case ScreenSelectionMode.Screen:
                {
                    var primaryHeight = NSScreen.Screens[0].Frame.Height;
                    var screen = NSScreen.Screens.FirstOrDefault(s =>
                    {
                        var quartzRect = new CGRect(
                            s.Frame.X,
                            primaryHeight - (s.Frame.Y + s.Frame.Height),
                            s.Frame.Width,
                            s.Frame.Height);
                        return quartzRect.Contains(point);
                    });

                    _selectedElement = screen is null ? null : new NSScreenVisualElement(screen);

                    if (_selectedElement is not null)
                    {
                        maskRect = _selectedElement.BoundingRectangle;
                    }
                    break;
                }
                case ScreenSelectionMode.Window:
                {
                    _selectedElement = GetElementAtPoint();

                    // Traverse up to find the containing window element
                    while (_selectedElement is AXUIElement axui && axui.Role != AXRoleAttribute.AXWindow)
                    {
                        _selectedElement = _selectedElement.Parent;
                    }

                    if (_selectedElement == null) break;

                    maskRect = _selectedElement.BoundingRectangle;
                    break;
                }
                case ScreenSelectionMode.Element:
                {
                    _selectedElement = GetElementAtPoint();
                    if (_selectedElement == null) break;

                    maskRect = _selectedElement.BoundingRectangle;
                    break;
                }
            }

            foreach (var maskWindow in MaskWindows) maskWindow.SetMask(maskRect);
            ToolTipWindow.ToolTip.Element = _selectedElement;

            AXUIElement? GetElementAtPoint()
            {
                _windowOwnerPids.Clear();
                GetWindowOwnerPidsAtLocation(point, GetWindowNumber(), _windowOwnerPids);
                foreach (var windowOwnerPid in _windowOwnerPids)
                {
                    if (AXUIElement.ElementFromPid(windowOwnerPid) is not { } element) continue;

                    if (element.ElementAtPosition((float)point.X, (float)point.Y) is { } foundElement)
                    {
                        return foundElement;
                    }
                }

                return null;
            }
        }
    }

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGWindowListCopyWindowInfo(CGWindowListOption option, uint relativeToWindow);

    private static void GetWindowOwnerPidsAtLocation(CGPoint point, uint relativeToWindow, List<int> pids)
    {
        var currentPid = Environment.ProcessId;
        var pArray = CGWindowListCopyWindowInfo(CGWindowListOption.OnScreenBelowWindow, relativeToWindow);
        if (pArray == IntPtr.Zero) return;

        try
        {
            var array = Runtime.GetNSObject<NSArray>(pArray);
            if (array == null) return;

            using var kOwnerPid = new NSString("kCGWindowOwnerPID");
            using var kBounds = new NSString("kCGWindowBounds");
            using var kX = new NSString("X");
            using var kY = new NSString("Y");
            using var kW = new NSString("Width");
            using var kH = new NSString("Height");

            for (nuint i = 0; i < array.Count; i++)
            {
                var dict = array.GetItem<NSDictionary>(i);
                if (dict == null) continue;

                if (!dict.TryGetValue(kOwnerPid, out var pidObj) || pidObj is not NSNumber pidNum) continue;

                // Filter out our own process (Picker, Mask, ToolTip, etc.)
                if (pidNum.Int32Value == currentPid) continue;

                if (!dict.TryGetValue(kBounds, out var boundsObj) || boundsObj is not NSDictionary boundsDict) continue;
                if (!boundsDict.TryGetValue(kX, out var xObj) || xObj is not NSNumber x ||
                    !boundsDict.TryGetValue(kY, out var yObj) || yObj is not NSNumber y ||
                    !boundsDict.TryGetValue(kW, out var wObj) || wObj is not NSNumber w ||
                    !boundsDict.TryGetValue(kH, out var hObj) || hObj is not NSNumber h) continue;

                var rect = new CGRect(x.DoubleValue, y.DoubleValue, w.DoubleValue, h.DoubleValue);
                if (rect.Contains(point))
                {
                    pids.Add(pidNum.Int32Value);
                }
            }
        }
        finally
        {
            CoreFoundationInterop.CFRelease(pArray);
        }
    }
}