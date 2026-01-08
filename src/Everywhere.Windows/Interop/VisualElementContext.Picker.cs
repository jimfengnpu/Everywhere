using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Everywhere.Interop;
using Point = System.Drawing.Point;

namespace Everywhere.Windows.Interop;

/// <summary>
/// A utility class for picking visual elements from the screen.
/// </summary>
public partial class VisualElementContext
{
    /// <summary>
    /// A window that allows the user to pick an element from the screen.
    /// </summary>
    private sealed class VisualElementPicker : ScreenSelectionSession
    {
        private static ScreenSelectionMode _previousMode = ScreenSelectionMode.Element;

        public static Task<IVisualElement?> PickAsync(IWindowHelper windowHelper, ScreenSelectionMode? initialMode)
        {
            var window = new VisualElementPicker(windowHelper, initialMode ?? _previousMode);
            window.Show();
            return window._pickingPromise.Task;
        }

        /// <summary>
        /// A promise that resolves to the picked visual element.
        /// </summary>
        private readonly TaskCompletionSource<IVisualElement?> _pickingPromise = new();

        private IVisualElement? _selectedElement;

        private VisualElementPicker(IWindowHelper windowHelper, ScreenSelectionMode initialMode)
            : base(windowHelper, [ScreenSelectionMode.Screen, ScreenSelectionMode.Window, ScreenSelectionMode.Element], initialMode)
        {
        }

        protected override void OnCanceled()
        {
            _selectedElement = null;
        }

        protected override void OnCloseCleanup()
        {
            _previousMode = CurrentMode;
            _pickingPromise.TrySetResult(_selectedElement);
        }

        protected override void OnMove(Point point)
        {
            var maskRect = new PixelRect();
            var pixelPoint = new PixelPoint(point.X, point.Y);
            switch (CurrentMode)
            {
                case ScreenSelectionMode.Screen:
                {
                    var screen = Screens.All.FirstOrDefault(s => s.Bounds.Contains(pixelPoint));
                    if (screen == null) break;

                    var hMonitor = PInvoke.MonitorFromPoint(point, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                    if (hMonitor == HMONITOR.Null) break;

                    _selectedElement = new ScreenVisualElementImpl(hMonitor);
                    maskRect = screen.Bounds;
                    break;
                }
                case ScreenSelectionMode.Window:
                {
                    var selectedHWnd = PInvoke.WindowFromPoint(point);
                    if (selectedHWnd == HWND.Null) break;

                    var rootHWnd = PInvoke.GetAncestor(selectedHWnd, GET_ANCESTOR_FLAGS.GA_ROOTOWNER);
                    if (rootHWnd == HWND.Null) break;

                    _selectedElement = TryCreateVisualElement(() => Automation.FromHandle(rootHWnd));
                    if (_selectedElement == null) break;

                    maskRect = _selectedElement.BoundingRectangle;
                    break;
                }
                case ScreenSelectionMode.Element:
                {
                    // BUG: sometimes this only picks the window, not the element under the cursor (e.g. QQ)
                    _selectedElement = TryCreateVisualElement(() => Automation.FromPoint(point));
                    if (_selectedElement == null) break;

                    maskRect = _selectedElement.BoundingRectangle;
                    break;
                }
            }

            foreach (var maskWindow in MaskWindows) maskWindow.SetMask(maskRect);
            ToolTipWindow.ToolTip.Element = _selectedElement;
        }
    }
}