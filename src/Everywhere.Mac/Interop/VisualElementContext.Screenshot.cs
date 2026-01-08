using Avalonia;
using Avalonia.Media.Imaging;
using Everywhere.Interop;
using Everywhere.Utilities;
using ImageIO;

namespace Everywhere.Mac.Interop;

public partial class VisualElementContext
{
    private sealed class ScreenshotPicker : ScreenSelectionSession
    {
        public static Task<Bitmap?> ScreenshotAsync(IWindowHelper windowHelper, ScreenSelectionMode? initialMode)
        {
            var window = new ScreenshotPicker(windowHelper, initialMode ?? ScreenSelectionMode.Element);
            window.Show();
            return window._pickingPromise.Task;
        }

        private readonly TaskCompletionSource<Bitmap?> _pickingPromise = new();
        private Bitmap? _resultBitmap;

        private readonly DisposeCollector _disposables = new();

        /// <summary>
        /// We reuse a single list to avoid allocations during picking.
        /// </summary>
        private readonly List<int> _windowOwnerPids = [];

        private IVisualElement? _selectedElement;

        // Free Mode State
        private bool _isDragging;
        private CGPoint _dragStart;
        private PixelRect _dragRect;

        private ScreenshotPicker(IWindowHelper windowHelper, ScreenSelectionMode initialMode)
            : base(
                windowHelper,
                [ScreenSelectionMode.Screen, ScreenSelectionMode.Window, ScreenSelectionMode.Element, ScreenSelectionMode.Free],
                initialMode)
        {
            CaptureAndSetBackground();
        }

        private void CaptureAndSetBackground()
        {
            var screens = NSScreen.Screens;
            for (var i = 0; i < screens.Length; i++)
            {
                if (i >= MaskWindows.Length) break;

                var screen = screens[i];
                var maskWindow = MaskWindows[i];

                if (CaptureScreen(screen.Frame) is not { } bitmap) continue;

                maskWindow.SetImage(bitmap);
                _disposables.Add(bitmap);
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

            _pickingPromise.TrySetResult(_resultBitmap);
        }

        protected override void OnLeftButtonDown()
        {
            if (CurrentMode == ScreenSelectionMode.Free)
            {
                _dragStart = CurrentMouseLocation; // Cocoa coords (bottom-left)
                // But CurrentMouseLocation is updated in OnPointerMoved.
                // ScreenSelectionSession.CurrentMouseLocation is updated via NSEvent.CurrentMouseLocation (Cocoa)

                _isDragging = true;
                _dragRect = new PixelRect(0, 0, 0, 0);

                // However, OnMove logic uses Quartz point.
                // Let's rely on OnMove to convert and update drag logic if we track drag start in Quartz?

                var primaryScreenHeight = NSScreen.Screens[0].Frame.Height;
                var quartzStart = new CGPoint(_dragStart.X, primaryScreenHeight - _dragStart.Y);

                // Update internal state
                _dragStart = quartzStart; // Store as quartz for consistency with OnMove?

                var dragRect = new PixelRect((int)quartzStart.X, (int)quartzStart.Y, 0, 0);
                foreach (var maskWindow in MaskWindows) maskWindow.SetMask(dragRect);
                UpdateToolTipInfo(dragRect);
            }
        }

        protected override bool OnLeftButtonUp()
        {
            PixelRect captureRect;

            if (CurrentMode == ScreenSelectionMode.Free)
            {
                if (!_isDragging) return false;
                _isDragging = false;
                captureRect = _dragRect;
                if (captureRect.Width <= 0 || captureRect.Height <= 0) return false;
            }
            else
            {
                if (_selectedElement == null) return false;
                captureRect = _selectedElement.BoundingRectangle;
            }

            _resultBitmap = CaptureScreen(captureRect);
            return true;
        }

        protected override void OnMove(CGPoint point)
        {
            if (CurrentMode == ScreenSelectionMode.Free)
            {
                if (_isDragging)
                {
                    // point is Quartz
                    var startX = _dragStart.X;
                    var startY = _dragStart.Y;

                    var minX = Math.Min(startX, point.X);
                    var minY = Math.Min(startY, point.Y);
                    var maxX = Math.Max(startX, point.X);
                    var maxY = Math.Max(startY, point.Y);

                    _dragRect = new PixelRect((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));

                    foreach (var maskWindow in MaskWindows) maskWindow.SetMask(_dragRect);
                    UpdateToolTipInfo(_dragRect);
                }
                else
                {
                    foreach (var maskWindow in MaskWindows) maskWindow.SetMask(new PixelRect(0, 0, 0, 0));

                    ToolTipWindow.ToolTip.SizeInfo = null;
                }
            }
            else
            {
                // Reuse element picking logic
                _isDragging = false;

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

                        if (screen != null)
                        {
                            // Temporary element just for rect
                            var el = new NSScreenVisualElement(screen);
                            _selectedElement = el; // used for screenshot capture later
                            maskRect = el.BoundingRectangle;
                        }
                        break;
                    }
                    case ScreenSelectionMode.Window:
                    {
                        _selectedElement = GetElementAtPoint();
                        while (_selectedElement is AXUIElement axui && axui.Role != AXRoleAttribute.AXWindow)
                        {
                            _selectedElement = _selectedElement.Parent;
                        }
                        if (_selectedElement != null) maskRect = _selectedElement.BoundingRectangle;
                        break;
                    }
                    case ScreenSelectionMode.Element:
                    {
                        _selectedElement = GetElementAtPoint();
                        if (_selectedElement != null) maskRect = _selectedElement.BoundingRectangle;
                        break;
                    }
                }

                foreach (var maskWindow in MaskWindows) maskWindow.SetMask(maskRect);
                ToolTipWindow.ToolTip.Element = _selectedElement;
                UpdateToolTipInfo(maskRect);
            }

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

        private void UpdateToolTipInfo(PixelRect rect)
        {
            ToolTipWindow.ToolTip.SizeInfo = $"{rect.Width} x {rect.Height}";
        }

        private static Bitmap? CaptureScreen(PixelRect rect)
        {
            return CaptureScreen(new CGRect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        private static Bitmap? CaptureScreen(CGRect rect)
        {
            if (rect.IsEmpty) return null;

#pragma warning disable CA1422 // Validate platform compatibility
            // ReSharper disable once MethodIsTooComplex
            using var cgImage = CGImage.ScreenImage(
                0,
                rect,
                CGWindowListOption.OnScreenOnly,
                CGWindowImageOption.Default);
#pragma warning restore CA1422

            if (cgImage is null) return null;

            using var data = new NSMutableData();
            using var dest = CGImageDestination.Create(data, "public.png", 1);

            if (dest is null) return null;

            dest.AddImage(cgImage);
            dest.Close();
            return new Bitmap(data.AsStream());
        }
    }
}