using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Media.Imaging;
using Everywhere.Interop;
using Everywhere.Utilities;
using Point = System.Drawing.Point;

namespace Everywhere.Windows.Interop;

public partial class VisualElementContext
{
    private sealed class ScreenshotPicker : ScreenSelectionSession
    {
        private static ScreenSelectionMode _previousMode = ScreenSelectionMode.Element;

        public static Task<Bitmap?> ScreenshotAsync(IWindowHelper windowHelper, ScreenSelectionMode? initialMode)
        {
            // Start with Screen mode by default, or maybe Free?
            var window = new ScreenshotPicker(windowHelper, initialMode ?? _previousMode);
            window.Show();
            return window._pickingPromise.Task;
        }

        private readonly TaskCompletionSource<Bitmap?> _pickingPromise = new();
        private readonly DisposeCollector _disposables = new();

        private Bitmap? _resultBitmap;
        private IVisualElement? _selectedElement;

        // Free Mode State
        private bool _isDragging;
        private PixelPoint _dragStart;
        private PixelRect _dragRect;

        private ScreenshotPicker(IWindowHelper windowHelper, ScreenSelectionMode initialMode)
            : base(
                windowHelper,
                [ScreenSelectionMode.Screen, ScreenSelectionMode.Window, ScreenSelectionMode.Element, ScreenSelectionMode.Free],
                initialMode)
        {
            // Freeze screen for better screenshot experience
            CaptureAndSetBackground();
        }

        private void CaptureAndSetBackground()
        {
            // We need to capture each screen and set it to the corresponding MaskWindow
            var screens = Screens.All;

            for (var i = 0; i < screens.Count; i++)
            {
                if (i >= MaskWindows.Length) break;

                var screen = screens[i];
                var maskWindow = MaskWindows[i];

                // Capture full screen content
                // Note: using System.Drawing for capture, then converting to Avalonia Bitmap
                // This might be heavy if many screens or high res, but it is necessary for "freeze" effect.
                try
                {
                    var bitmap = CaptureScreen(screen.Bounds);
                    maskWindow.SetImage(bitmap);
                    _disposables.Add(bitmap);
                }
                catch
                {
                    // If capture fails, we just don't show the background image (fallback to transparent/dimmed)
                }
            }
        }

        protected override void OnCanceled()
        {
            _resultBitmap = null;
        }

        protected override void OnCloseCleanup()
        {
            foreach (var maskWindow in MaskWindows)
            {
                maskWindow.SetImage(null);
            }

            _disposables.Dispose();

            _previousMode = CurrentMode;
            _pickingPromise.TrySetResult(_resultBitmap);
        }

        protected override void OnLeftButtonDown()
        {
            // If in Free mode, start dragging
            if (CurrentMode != ScreenSelectionMode.Free) return;

            PInvoke.GetCursorPos(out var point);
            _dragStart = new PixelPoint(point.X, point.Y);
            _isDragging = true;
            _dragRect = new PixelRect(_dragStart, new PixelSize(0, 0));

            // Update visuals
            foreach (var maskWindow in MaskWindows) maskWindow.SetMask(_dragRect);
            UpdateToolTipInfo(_dragRect);
        }

        protected override bool OnLeftButtonUp()
        {
            PixelRect captureRect;

            if (CurrentMode == ScreenSelectionMode.Free)
            {
                if (!_isDragging) return false; // Clicked without dragging? Maybe treat as single pixel point or ignore?
                _isDragging = false;
                captureRect = _dragRect;
                if (captureRect.Width <= 0 || captureRect.Height <= 0) return false; // Too small
            }
            else
            {
                // Other modes
                if (_selectedElement == null) return false;
                captureRect = _selectedElement.BoundingRectangle;
            }

            // Hide ToolTip and capture
            WindowHelper.SetCloaked(ToolTipWindow, true);
            _resultBitmap = CaptureScreen(captureRect);
            return true; // Close
        }

        protected override void OnMove(Point point)
        {
            var pixelPoint = new PixelPoint(point.X, point.Y);

            if (CurrentMode == ScreenSelectionMode.Free)
            {
                if (_isDragging)
                {
                    // Update Drag Rect
                    var topLeft = new PixelPoint(Math.Min(_dragStart.X, pixelPoint.X), Math.Min(_dragStart.Y, pixelPoint.Y));
                    var bottomRight = new PixelPoint(Math.Max(_dragStart.X, pixelPoint.X), Math.Max(_dragStart.Y, pixelPoint.Y));
                    _dragRect = new PixelRect(topLeft, bottomRight); // Extension or constructor?
                    // PixelRect constructor takes Point, Size.
                    _dragRect = new PixelRect(topLeft, new PixelSize(bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y));

                    foreach (var maskWindow in MaskWindows) maskWindow.SetMask(_dragRect);
                    UpdateToolTipInfo(_dragRect);
                }
                else
                {
                    // No mask when just hovering in Free Mode?
                    // Or maybe a crosshair?
                    // The mask window draws an overlay, if we pass empty rect, it might mask everything?
                    // In current implementation `SetMask` excludes the rect from the dark overlay.
                    // If we want "Everything Dark", we set mask to Empty?
                    // `SetMask` impl: `_maskBorder.Clip = ... Exclude(maskRect)`.
                    // If maskRect is empty, it excludes nothing, so full dark.
                    foreach (var maskWindow in MaskWindows) maskWindow.SetMask(new PixelRect(0, 0, 0, 0));

                    ToolTipWindow.ToolTip.SizeInfo = null;
                }
            }
            else
            {
                // Logic from VisualElementPicker (Screen/Window/Element)
                // We can duplicate the logic or we should have pushed it to Base or Helper?
                // Duplicating for now as it accesses _selectedElement which is specific here (we need it for capture).

                // Reset Drag state if we switched modes while dragging (should handle in OnModeChanged but Update is enough)
                _isDragging = false;

                var maskRect = new PixelRect();
                switch (CurrentMode)
                {
                    case ScreenSelectionMode.Screen:
                    {
                        var screen = Screens.All.FirstOrDefault(s => s.Bounds.Contains(pixelPoint));
                        if (screen != null)
                        {
                            var hMonitor = PInvoke.MonitorFromPoint(point, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                            if (hMonitor != HMONITOR.Null)
                            {
                                // We don't necessarily need IVisualElement here if we just want rect, but consistent UI is good.
                                _selectedElement = new ScreenVisualElementImpl(hMonitor);
                                maskRect = screen.Bounds;
                            }
                        }
                        break;
                    }
                    case ScreenSelectionMode.Window:
                    {
                        var selectedHWnd = PInvoke.WindowFromPoint(point);
                        if (selectedHWnd != HWND.Null)
                        {
                            var rootHWnd = PInvoke.GetAncestor(selectedHWnd, GET_ANCESTOR_FLAGS.GA_ROOTOWNER);
                            if (rootHWnd != HWND.Null)
                            {
                                _selectedElement = TryCreateVisualElement(() => Automation.FromHandle(rootHWnd));
                                if (_selectedElement != null) maskRect = _selectedElement.BoundingRectangle;
                            }
                        }
                        break;
                    }
                    case ScreenSelectionMode.Element:
                    {
                        _selectedElement = TryCreateVisualElement(() => Automation.FromPoint(point));
                        if (_selectedElement != null) maskRect = _selectedElement.BoundingRectangle;
                        break;
                    }
                }

                foreach (var maskWindow in MaskWindows) maskWindow.SetMask(maskRect);
                ToolTipWindow.ToolTip.Element = _selectedElement;
                UpdateToolTipInfo(maskRect);
            }
        }

        private void UpdateToolTipInfo(PixelRect rect)
        {
            ToolTipWindow.ToolTip.SizeInfo = $"{rect.Width} x {rect.Height}";
        }
    }
}