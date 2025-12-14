using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Everywhere.Interop;
using Everywhere.Views;
using ObjCRuntime;
using ZLinq;

namespace Everywhere.Mac.Interop;

public partial class VisualElementContext
{
    private class VisualElementPicker : VisualElementPickerTransparentWindow
    {
        public static Task<IVisualElement?> PickAsync(IWindowHelper windowHelper, ElementPickMode mode)
        {
            var window = new VisualElementPicker(windowHelper, mode);
            window.Show();
            return window._pickingPromise.Task;
        }

        private readonly TaskCompletionSource<IVisualElement?> _pickingPromise = new();

        private readonly uint _windowNumber;
        private readonly CGRect _allScreenFrame;
        private readonly PixelRect _allScreenBounds;
        private readonly VisualElementPickerMaskWindow[] _maskWindows;
        private readonly VisualElementPickerToolTipWindow _toolTipWindow;

        /// <summary>
        /// We reuse a single list to avoid allocations during picking.
        /// </summary>
        private readonly List<int> _windowOwnerPids = [];

        private ElementPickMode _elementPickMode;
        private CGPoint _currentMouseLocation;
        private IVisualElement? _selectedElement;

        private VisualElementPicker(IWindowHelper windowHelper, ElementPickMode elementPickMode)
        {
            _elementPickMode = elementPickMode;

            var handle = TryGetPlatformHandle()?.Handle ?? 0;
            if (handle != 0)
            {
                using var nsWindow = new NSWindow();
                nsWindow.Handle = handle;
                _windowNumber = (uint)nsWindow.WindowNumber;
                nsWindow.Handle = 0;
            }

            // Use NSScreen to get accurate logical bounds and handle scaling
            var allScreens = NSScreen.Screens;
            // The primary screen (index 0) defines the coordinate space origin (0,0) in Cocoa.
            // We need its height to convert between Cocoa (Bottom-Left) and Quartz (Top-Left) coordinates.
            var primaryScreen = allScreens[0];
            var primaryScreenHeight = primaryScreen.Frame.Height;

            _maskWindows = new VisualElementPickerMaskWindow[allScreens.Length];
            for (var i = 0; i < allScreens.Length; i++)
            {
                var screen = allScreens[i];
                var frame = screen.Frame;
                _allScreenFrame = CGRect.Union(_allScreenFrame, frame);

                // Convert to Avalonia coordinates (top-left origin)
                // Quartz Y = PrimaryScreenHeight - (Cocoa Y + Height)
                var bounds = new PixelRect(
                    (int)frame.X,
                    (int)(primaryScreenHeight - (frame.Y + frame.Height)),
                    (int)frame.Width,
                    (int)frame.Height);
                _allScreenBounds = _allScreenBounds.Union(bounds);
                var maskWindow = new VisualElementPickerMaskWindow(bounds);
                SetNsWindowPlacement(maskWindow, frame);
                windowHelper.SetHitTestVisible(maskWindow, false);
                _maskWindows[i] = maskWindow;
            }

            SetPlacement(_allScreenBounds, out _); // Place window to (0, 0) in avalonia way

            _toolTipWindow = new VisualElementPickerToolTipWindow(elementPickMode);
            windowHelper.SetHitTestVisible(_toolTipWindow, false);
            SetNsWindowPlacement(_toolTipWindow, null);
        }

        private static void SetNsWindowPlacement(Window window, CGRect? frame)
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? 0;
            if (handle == 0) return;

            using var nsWindow = new NSWindow();
            nsWindow.Handle = handle;
            nsWindow.Level = NSWindowLevel.ScreenSaver + 1;
            if (frame.HasValue) nsWindow.SetFrame(frame.Value, true);
            nsWindow.Handle = 0;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // Place window to cover all screens
            // We must set after opened otherwise macOS may ignore the frame set in constructor
            SetNsWindowPlacement(this, _allScreenFrame);

            foreach (var maskWindow in _maskWindows) maskWindow.Show(this);
            _toolTipWindow.Show(this);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);

            e.Handled = true;
            e.Pointer.Capture(null);

            if (point.Properties.IsRightButtonPressed)
            {
                _selectedElement = null;
                Close();
                return;
            }

            if (point.Properties.IsLeftButtonPressed)
            {
                // Confirm selection
                Close();
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            _elementPickMode = (ElementPickMode)((int)(_elementPickMode + (e.Delta.Y > 0 ? -1 : 1)) switch
            {
                > 2 => 0,
                < 0 => 2,
                var v => v
            });
            HandlePickModeChanged();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    _selectedElement = null;
                    Close();
                    break;
                case Key.D1:
                case Key.NumPad1:
                case Key.F1:
                    _elementPickMode = ElementPickMode.Screen;
                    HandlePickModeChanged();
                    break;
                case Key.D2:
                case Key.NumPad2:
                case Key.F2:
                    _elementPickMode = ElementPickMode.Window;
                    HandlePickModeChanged();
                    break;
                case Key.D3:
                case Key.NumPad3:
                case Key.F3:
                    _elementPickMode = ElementPickMode.Element;
                    HandlePickModeChanged();
                    break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            HandlePointerMoved(_currentMouseLocation = NSEvent.CurrentMouseLocation);
        }

        protected override void OnClosed(EventArgs e)
        {
            _pickingPromise.TrySetResult(_selectedElement);
            base.OnClosed(e);
        }

        private void HandlePickModeChanged()
        {
            HandlePointerMoved(_currentMouseLocation);
            Dispatcher.UIThread.Post(() =>
            {
                _toolTipWindow.ToolTip.Mode = _elementPickMode;
            }, DispatcherPriority.Background);
        }

        private void HandlePointerMoved(CGPoint point)
        {
            // NSEvent.CurrentMouseLocation is in Cocoa coordinates (Bottom-Left origin).
            // We need to convert it to Quartz coordinates (Top-Left origin) for AX and WindowList.
            // The primary screen (index 0) defines the coordinate space origin.
            var primaryScreenHeight = NSScreen.Screens[0].Frame.Height;
            var quartzPoint = new CGPoint(point.X, primaryScreenHeight - point.Y);

            PickElement(quartzPoint);
            SetToolTipWindowPosition(quartzPoint);
        }

        private void SetToolTipWindowPosition(CGPoint pointerPoint)
        {
            const int margin = 16;

            // Use NSScreen to check bounds as we are working with logical points (CGPoint)
            var screen = NSScreen.Screens.AsValueEnumerable().FirstOrDefault(s =>
            {
                // NSScreen Frame is Cocoa (Bottom-Left). We need to convert our Quartz point back to check?
                // Or convert NSScreen Frame to Quartz.
                // Let's convert NSScreen Frame to Quartz for checking.
                var primaryHeight = NSScreen.Screens[0].Frame.Height;
                var quartzRect = new CGRect(
                    s.Frame.X,
                    primaryHeight - (s.Frame.Y + s.Frame.Height),
                    s.Frame.Width,
                    s.Frame.Height);
                return quartzRect.Contains(pointerPoint);
            });

            if (screen == null) return;

            // We need the bounds in Quartz coordinates for positioning logic
            var primaryScreenHeight = NSScreen.Screens[0].Frame.Height;
            var screenBounds = new CGRect(
                    screen.Frame.X,
                    primaryScreenHeight - (screen.Frame.Y + screen.Frame.Height),
                    screen.Frame.Width,
                    screen.Frame.Height);

            var tooltipSize = _toolTipWindow.Bounds.Size;

            var x = pointerPoint.X.Value;
            var y = pointerPoint.Y.Value - margin - tooltipSize.Height;

            // Check if there is enough space above the pointer
            if (y < screenBounds.Top) // Top is min Y in Quartz
            {
                y = pointerPoint.Y.Value + margin; // place below the pointer
            }

            // Check if there is enough space to the right of the pointer
            if (x + tooltipSize.Width > screenBounds.Right)
            {
                x = pointerPoint.X.Value - tooltipSize.Width; // place to the left of the pointer
            }

            _toolTipWindow.Position = new PixelPoint((int)x, (int)y);
        }

        private void PickElement(CGPoint point)
        {
            var maskRect = new PixelRect();
            switch (_elementPickMode)
            {
                case ElementPickMode.Screen:
                {
                    // VisualElementContext.ElementFromPoint expects PixelPoint but handles conversion internally?
                    // No, we modified it to take PixelPoint and use it directly.
                    // But wait, VisualElementContext.ElementFromPoint logic for Screen:
                    // var screen = NSScreen.Screens.FirstOrDefault(s => s.Frame.Contains(new CGPoint(point.X, point.Y)));
                    // This expects 'point' to be in Cocoa coordinates if it checks against s.Frame directly!

                    // Let's look at VisualElementContext.ElementFromPoint again.
                    // It was: var screen = NSScreen.Screens.FirstOrDefault(s => s.Frame.Contains(new CGPoint(point.X, point.Y)));
                    // NSScreen.Frame is Cocoa (Bottom-Left).
                    // If we pass Quartz point (Top-Left), this check will fail!

                    // So for Screen mode, we might need to pass Cocoa point or fix VisualElementContext.
                    // However, for Element/Window mode (AX), we need Quartz point.

                    // Let's fix Screen mode here locally since we have the Quartz point.
                    // We can find the screen using our Quartz logic.

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
                case ElementPickMode.Window:
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
                case ElementPickMode.Element:
                {
                    _selectedElement = GetElementAtPoint();
                    if (_selectedElement == null) break;

                    maskRect = _selectedElement.BoundingRectangle;
                    break;
                }
            }

            foreach (var maskWindow in _maskWindows) maskWindow.SetMask(maskRect);
            _toolTipWindow.ToolTip.Element = _selectedElement;

            AXUIElement? GetElementAtPoint()
            {
                _windowOwnerPids.Clear();
                GetWindowOwnerPidsAtLocation(point, _windowNumber, _windowOwnerPids);
                foreach (var windowOwnerPid in _windowOwnerPids)
                {
                    if (AXUIElement.ElementFromPid(windowOwnerPid) is not {} element) continue;

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