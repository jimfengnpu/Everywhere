using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Window = Avalonia.Controls.Window;
using Everywhere.Extensions;
using Everywhere.I18N;
using Everywhere.Interop;
using Everywhere.Views;

namespace Everywhere.Linux.Interop;

public partial class LinuxVisualElementContext
{
    private class ElementPicker : Window
    {
        public static Task<IVisualElement?> PickAsync(
            LinuxVisualElementContext context,
            ILinuxWindowBackend backend,
            PickElementMode mode)
        {
            var window = new ElementPicker(context, backend, mode);
            window.Show();
            return window._pickingPromise.Task;
        }
        /// <summary>
        /// A promise that resolves to the picked visual element.
        /// </summary>
        private readonly TaskCompletionSource<IVisualElement?> _pickingPromise = new();
        private readonly LinuxVisualElementContext _context;
        private readonly PickElementMode _pickMode;
        private readonly PixelRect _allScreenBounds;
        private readonly MaskWindow[] _maskWindows;
        private readonly ElementPickerToolTip _toolTip;
        private readonly Window _toolTipWindow;
        private IVisualElement? _selectedElement;

        private ElementPicker(
            LinuxVisualElementContext context,
            ILinuxWindowBackend backend,
            PickElementMode pickMode)
        {
            _context = context;
            _pickMode = pickMode;
            var allScreens = Screens.All;
            _maskWindows = new MaskWindow[allScreens.Count];
            for (var i = 0; i < allScreens.Count; i++)
            {
                var screen = allScreens[i];
                _allScreenBounds = _allScreenBounds.Union(screen.Bounds);
                var maskWindow = new MaskWindow(screen.Bounds);
                backend.SetHitTestVisible(maskWindow, false);
                _maskWindows[i] = maskWindow;
            }

            Background = Brushes.Transparent;
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
            SystemDecorations = SystemDecorations.None;
            SetWindowStyles(this);
            SetWindowPlacement(this, _allScreenBounds, out _);

            _toolTipWindow = new Window
            {
                Content = _toolTip = new ElementPickerToolTip
                {
                    Mode = pickMode
                },
                SizeToContent = SizeToContent.WidthAndHeight,
                SystemDecorations = SystemDecorations.BorderOnly,
                ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome,
                ExtendClientAreaToDecorationsHint = true,
            };
            SetWindowStyles(_toolTipWindow);
            backend.SetHitTestVisible(_toolTipWindow, false);
        }
        
        private static void SetWindowStyles(Window window)
        {
            window.Topmost = true;
            window.CanResize = false;
            window.ShowInTaskbar = false;
            window.SystemDecorations = SystemDecorations.None;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
        }
        
        private static void SetWindowPlacement(Window window, PixelRect screenBounds, out double scale)
        {
            window.Position = screenBounds.Position;
            scale = window.DesktopScaling; // we must set Position first to get the correct scaling factor
            window.Width = screenBounds.Width / scale;
            window.Height = screenBounds.Height / scale;
        }

        protected override void OnOpened(EventArgs e)
        {
            bool leftPressed = false;
            
            _context._eventHelper.WindowPickerHook(this, (point, type) =>
            {
                switch (type)
                {
                    case EventType.MouseDown:
                        if (!leftPressed)
                        {
                            leftPressed = true;
                            Dispatcher.UIThread.Post(() => PickElement(point), DispatcherPriority.Default);
                        }
                        break;
                    case EventType.MouseDrag:
                        if (leftPressed)
                        { 
                            Dispatcher.UIThread.Post(() => PickElement(point), DispatcherPriority.Default);
                        }
                        break;
                    case EventType.MouseUp:
                        leftPressed = false;
                        Dispatcher.UIThread.Post(Close, DispatcherPriority.Default);
                        break;
                }
            });
        }
        
        protected override void OnClosed(EventArgs e)
        {
            _context._eventHelper.UngrabMouseHook();
            _pickingPromise.TrySetResult(_selectedElement);
        }

        private void PickElement(PixelPoint pixelPoint)
        {
            SetToolTipWindowPosition(pixelPoint);
            if (_selectedElement != null && _selectedElement.BoundingRectangle.Contains(pixelPoint))
            {
                return;
            }
            _selectedElement = _context.ElementFromPoint(pixelPoint, _pickMode);
            if (_selectedElement == null) return;
            var maskRect = _selectedElement.BoundingRectangle;
            foreach (var maskWindow in _maskWindows) maskWindow.SetMask(maskRect);
            _toolTip.Header = GetElementDescription(_selectedElement);
        }
        
        /// <summary>
        /// Set the position of the tooltip window based on the pointer position.
        /// </summary>
        /// <remarks>
        /// The margin between the pointer and the tooltip window is 16 pixels.
        /// It trys to keep the tooltip window within the screen bounds.
        /// Default: left edge of the tooltip is aligned with the pointer, above the pointer.
        /// </remarks>
        /// <param name="pointerPoint"></param>
        private void SetToolTipWindowPosition(PixelPoint pointerPoint)
        {
            const int margin = 16;

            var screen = Screens.All.FirstOrDefault(s => s.Bounds.Contains(pointerPoint));
            if (screen == null) return;

            var screenBounds = screen.Bounds;
            var tooltipSize = _toolTipWindow.Bounds.Size * _toolTipWindow.DesktopScaling;

            var x = (double)pointerPoint.X;
            var y = pointerPoint.Y - margin - tooltipSize.Height;

            // Check if there is enough space above the pointer
            if (y < 0d)
            {
                y = pointerPoint.Y + margin; // place below the pointer
            }

            // Check if there is enough space to the right of the pointer
            if (x + tooltipSize.Width > screenBounds.Right)
            {
                x = pointerPoint.X - tooltipSize.Width; // place to the left of the pointer
            }

            _toolTipWindow.Position = new PixelPoint((int)x, (int)y);
        }

        private readonly Dictionary<int, string> _processNameCache = new();

        private string? GetElementDescription(IVisualElement? element)
        {
            if (element is null) return LocaleKey.Common_None;

            DynamicResourceKey key;
            var elementTypeKey = new DynamicResourceKey($"VisualElementType_{element.Type}");
            if (element.ProcessId != 0)
            {
                if (!_processNameCache.TryGetValue(element.ProcessId, out var processName))
                {
                    try
                    {
                        using var process = Process.GetProcessById(element.ProcessId);
                        processName = process.ProcessName;
                    }
                    catch
                    {
                        processName = string.Empty;
                    }
                    _processNameCache[element.ProcessId] = processName;
                }

                key = processName.IsNullOrWhiteSpace() ?
                    elementTypeKey :
                    new FormattedDynamicResourceKey("{0} - {1}", new DirectResourceKey(processName), elementTypeKey);
            }
            else
            {
                key = elementTypeKey;
            }

            return key.ToString();
        }

        /// <summary>
        /// Mask window that displays the overlay during element picking.
        /// </summary>
        private sealed class MaskWindow : Window
        {
            private readonly Border _maskBorder;
            private readonly Border _elementBoundsBorder;
            private readonly PixelRect _screenBounds;
            private readonly double _scale;

            public MaskWindow(PixelRect screenBounds)
            {
                Content = new Panel
                {
                    IsHitTestVisible = false,
                    Children =
                    {
                        (_maskBorder = new Border
                        {
                            Background = Brushes.Black,
                            Opacity = 0.4
                        }),
                        (_elementBoundsBorder = new Border
                        {
                            BorderThickness = new Thickness(2),
                            BorderBrush = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Top
                        })
                    }
                };

                SetWindowStyles(this);
                Background = Brushes.Transparent;
                Cursor = new Cursor(StandardCursorType.Cross);
                TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
                SystemDecorations = SystemDecorations.None;

                _screenBounds = screenBounds;
                SetWindowPlacement(this, screenBounds, out _scale);
            }

            public void SetMask(PixelRect rect)
            {
                var maskRect = rect.Translate(-(PixelVector)_screenBounds.Position).ToRect(_scale);
                _maskBorder.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(Bounds), new RectangleGeometry(maskRect));
                _elementBoundsBorder.Margin = new Thickness(maskRect.X, maskRect.Y, 0, 0);
                _elementBoundsBorder.Width = maskRect.Width;
                _elementBoundsBorder.Height = maskRect.Height;
            }
        }
    }
}