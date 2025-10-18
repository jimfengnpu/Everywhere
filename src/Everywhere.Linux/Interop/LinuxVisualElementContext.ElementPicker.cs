namespace Everywhere.Linux.Interop;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Everywhere.Interop;
using Point = System.Drawing.Point;

public partial class LinuxVisualElementContext
{
    private class ElementPicker : Window
    {
        private readonly LinuxVisualElementContext _context;
        private readonly INativeHelper _nativeHelper;
        private readonly PickElementMode _mode;

        private readonly PixelRect _screenBounds;
        private readonly Bitmap _bitmap;
        private readonly Border _clipBorder;
        private readonly Image _image;
        private readonly double _scale;
        private readonly TaskCompletionSource<IVisualElement?> _taskCompletionSource = new();

        private Rect? _previousMaskRect;
        private IVisualElement? _selectedElement;

        private ElementPicker(
            LinuxVisualElementContext context,
            INativeHelper nativeHelper,
            PickElementMode mode)
        {
            _context = context;
            _nativeHelper = nativeHelper;
            _mode = mode;

            var allScreens = Screens.All;
            _screenBounds = allScreens.Aggregate(default(PixelRect), (current, screen) => current.Union(screen.Bounds));
            if (_screenBounds.Width <= 0 || _screenBounds.Height <= 0)
            {
                throw new InvalidOperationException("No valid screen bounds found.");
            }

            _bitmap = context._backend.Capture(null, _screenBounds);
            _clipBorder = new Border
            {
                ClipToBounds = false,
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            Content = new Panel
            {
                IsHitTestVisible = false,
                Children =
                {
                    new Image { Source = _bitmap },
                    new Border
                    {
                        Background = Brushes.Black,
                        Opacity = 0.4
                    },
                    (_image = new Image { Source = _bitmap }),
                    _clipBorder
                }
            };

            Topmost = true;
            CanResize = false;
            ShowInTaskbar = false;
            Cursor = new Cursor(StandardCursorType.Cross);
            SystemDecorations = SystemDecorations.None;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Position = _screenBounds.Position;
            _scale = DesktopScaling; // we must set Position first to get the correct scaling factor
            Width = _screenBounds.Width / _scale;
            Height = _screenBounds.Height / _scale;
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            // SendInput MouseLeftButtonDown, this will:
            // 1. prevent the cursor from changing to the default arrow cursor and interacting with other windows
            // 2. Trigger the OnPointerPressed event to set the window to hit test invisible
            _nativeHelper.SetWindowHitTestInvisible(this);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            var p = _context._backend.GetPointer();
            Pick(new Point(p.X, p.Y));
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            var p = _context._backend.GetPointer();
            Pick(new Point(p.X, p.Y));
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton != MouseButton.Left)
            {
                _selectedElement = null;
            }

            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) _selectedElement = null;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _bitmap.Dispose();
            _taskCompletionSource.TrySetResult(_selectedElement);
        }

        private void Pick(Point point)
        {
            var maskRect = new Rect();
            var pixelPoint = new PixelPoint(point.X, point.Y);
            switch (_mode)
            {
                case PickElementMode.Screen:
                    {
                        var screen = Screens.All.FirstOrDefault(s => s.Bounds.Contains(pixelPoint));
                        if (screen == null) break;

                        _selectedElement = _context.ElementFromPoint(pixelPoint, PickElementMode.Screen);
                        if (_selectedElement == null) break;

                        maskRect = screen.Bounds.Translate(-(PixelVector)_screenBounds.Position).ToRect(_scale);
                        break;
                    }
                case PickElementMode.Window:
                    {
                        _selectedElement = _context.ElementFromPoint(pixelPoint, PickElementMode.Window);
                        if (_selectedElement == null) break;

                        maskRect = _selectedElement.BoundingRectangle.Translate(-(PixelVector)_screenBounds.Position).ToRect(_scale);
                        break;
                    }
                case PickElementMode.Element:
                    {
                        _selectedElement = _context.ElementFromPoint(pixelPoint, PickElementMode.Element);
                        if (_selectedElement == null) break;

                        maskRect = _selectedElement.BoundingRectangle.Translate(-(PixelVector)_screenBounds.Position).ToRect(_scale);
                        break;
                    }
            }

            SetMask(maskRect);
        }

        private void SetMask(Rect rect)
        {
            if (_previousMaskRect == rect) return;

            _image.Clip = new RectangleGeometry(rect);
            _clipBorder.Margin = new Thickness(rect.X, rect.Y, 0, 0);
            _clipBorder.Width = rect.Width;
            _clipBorder.Height = rect.Height;

            _previousMaskRect = rect;
        }

        public static Task<IVisualElement?> PickAsync(
            LinuxVisualElementContext context,
            INativeHelper nativeHelper,
            PickElementMode mode)
        {
            var window = new ElementPicker(context, nativeHelper, mode);
            window.Show();
            return window._taskCompletionSource.Task;
        }
    }
}