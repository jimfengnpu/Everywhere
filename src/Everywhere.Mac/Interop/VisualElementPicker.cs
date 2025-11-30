using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Everywhere.Extensions;
using Everywhere.I18N;
using Everywhere.Interop;
using ShadUI;
using ZLinq;
using Window = Avalonia.Controls.Window;

namespace Everywhere.Mac.Interop;

public partial class VisualElementContext
{
    private class VisualElementPicker : Window
    {
        public static Task<IVisualElement?> PickAsync(IWindowHelper windowHelper, PickElementMode mode)
        {
            var window = new VisualElementPicker(windowHelper, mode);
            window.Show();
            return window._pickingPromise.Task;
        }

        private readonly TaskCompletionSource<IVisualElement?> _pickingPromise = new();

        private readonly PixelRect _screenBounds;
        private readonly Border _maskBorder;
        private readonly Border _elementBoundsBorder;

        private readonly Window _tooltipWindow;
        private readonly TextBlock _elementNameTextBlock;
        private readonly Badge _screenPickModeBadge;
        private readonly Badge _windowPickModeBadge;
        private readonly Badge _elementPickModeBadge;

        private PickElementMode _pickMode;
        private Rect? _previousMaskRect;
        private Point _currentPointerPosition;
        private IVisualElement? _selectedElement;

        private VisualElementPicker(IWindowHelper windowHelper, PickElementMode pickMode)
        {
            _pickMode = pickMode;

            // Use NSScreen to get accurate logical bounds and handle scaling
            var screens = NSScreen.Screens;
            var unionRect = screens
                .AsValueEnumerable()
                .Aggregate(CGRect.Empty, (current, screen) => current == CGRect.Empty ? screen.Frame : CGRect.Union(current, screen.Frame));

            // Convert to Avalonia coordinates (Top-Left origin)
            // Primary screen is screens[0]
            var primaryHeight = screens[0].Frame.Height;
            var avaloniaX = unionRect.X;
            var avaloniaY = primaryHeight - (unionRect.Y + unionRect.Height);

            // Set _screenBounds for coordinate calculations later
            _screenBounds = new PixelRect(
                (int)avaloniaX,
                (int)avaloniaY,
                (int)unionRect.Width,
                (int)unionRect.Height);

            Content = new Panel
            {
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

            Position = _screenBounds.Position;
            Width = unionRect.Width;
            Height = unionRect.Height;

            _tooltipWindow = new Window
            {
                TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent],
                ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome,
                ExtendClientAreaToDecorationsHint = true,
                Content = new ExperimentalAcrylicBorder
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 6),
                    Material = new ExperimentalAcrylicMaterial
                    {
                        FallbackColor = Color.FromArgb(153, 0, 0, 0),
                        MaterialOpacity = 0.7,
                        TintColor = Color.FromArgb(119, 34, 34, 34),
                        TintOpacity = 0.7
                    },
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Spacing = 4d,
                        Children =
                        {
                            (_elementNameTextBlock = new TextBlock
                            {
                                FontWeight = FontWeight.Bold,
                                Foreground = Brushes.White
                            }),
                            new TextBlock
                            {
                                Foreground = Brushes.White,
                                Text = LocaleResolver.VisualElementPicker_ToolTipWindow_TipTextBlock_Text
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 4d,
                                Children =
                                {
                                    (_screenPickModeBadge = new Badge
                                    {
                                        Background = Brushes.DimGray,
                                        Content = LocaleResolver.VisualElementPicker_ToolTipWindow_ScreenPickModeBadge_Content
                                    }),
                                    (_windowPickModeBadge = new Badge
                                    {
                                        Background = Brushes.DimGray,
                                        Content = LocaleResolver.VisualElementPicker_ToolTipWindow_WindowPickModeBadge_Content
                                    }),
                                    (_elementPickModeBadge = new Badge
                                    {
                                        Background = Brushes.DimGray,
                                        Content = LocaleResolver.VisualElementPicker_ToolTipWindow_ElementPickModeBadge_Content
                                    })
                                }
                            }
                        }
                    }
                }
            };

            SetWindowStyles(_tooltipWindow);
            windowHelper.SetHitTestVisible(_tooltipWindow, false);
            _tooltipWindow.SizeToContent = SizeToContent.WidthAndHeight;
            SetPickModeTextBlockStyles();
        }

        private static void SetWindowStyles(Window window)
        {
            window.Topmost = true;
            window.CanResize = false;
            window.ShowInTaskbar = false;
            window.SystemDecorations = SystemDecorations.None;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
        }

        private void SetPickModeTextBlockStyles()
        {
            var (t, f0, f1) = _pickMode switch
            {
                PickElementMode.Screen => (_screenPickModeBadge, _windowPickModeBadge, _elementPickModeBadge),
                PickElementMode.Window => (_windowPickModeBadge, _screenPickModeBadge, _elementPickModeBadge),
                _ => (_elementPickModeBadge, _screenPickModeBadge, _windowPickModeBadge),
            };

            t.Background = Brushes.DarkGreen;
            t.SetValue(TextElement.FontWeightProperty, FontWeight.Bold);
            f0.Background = f1.Background = Brushes.DimGray;
            f0.SetValue(TextElement.FontWeightProperty, FontWeight.Normal);
            f1.SetValue(TextElement.FontWeightProperty, FontWeight.Normal);
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            _tooltipWindow.Show();

            try
            {
                if (TryGetPlatformHandle()?.Handle is { } handle)
                {
                    using var nsWindow = NSWindow.FromWindowRef(handle);
                    nsWindow.Level = NSWindowLevel.ScreenSaver;
                }

                if (_tooltipWindow.TryGetPlatformHandle()?.Handle is { } tooltipHandle)
                {
                    using var nsTooltipWindow = NSWindow.FromWindowRef(tooltipHandle);
                    nsTooltipWindow.Level = NSWindowLevel.ScreenSaver + 1;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex); // BUG: NSWindow init error
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
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
            _pickMode = (PickElementMode)((int)(_pickMode + (e.Delta.Y > 0 ? -1 : 1)) switch
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
                    _pickMode = PickElementMode.Screen;
                    HandlePickModeChanged();
                    break;
                case Key.D2:
                case Key.NumPad2:
                case Key.F2:
                    _pickMode = PickElementMode.Window;
                    HandlePickModeChanged();
                    break;
                case Key.D3:
                case Key.NumPad3:
                case Key.F3:
                    _pickMode = PickElementMode.Element;
                    HandlePickModeChanged();
                    break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            HandlePointerMoved(_currentPointerPosition = e.GetPosition(this));
        }

        protected override void OnClosed(EventArgs e)
        {
            _tooltipWindow.Close();
            _pickingPromise.TrySetResult(_selectedElement);
            base.OnClosed(e);
        }

        private void HandlePickModeChanged()
        {
            SetPickModeTextBlockStyles();
            HandlePointerMoved(_currentPointerPosition);
        }

        private void HandlePointerMoved(Point point)
        {
            // Convert to screen coordinates
            var x = (int)point.X + _screenBounds.X;
            var y = (int)point.Y + _screenBounds.Y;
            var screenPoint = new PixelPoint(x, y);

            PickElement(screenPoint);
            SetToolTipWindowPosition(screenPoint);
        }

        private void SetToolTipWindowPosition(PixelPoint pointerPoint)
        {
            const int margin = 16;

            var screen = Screens.All.FirstOrDefault(s => s.Bounds.Contains(pointerPoint));
            if (screen == null) return;

            var screenBounds = screen.Bounds;
            var tooltipSize = _tooltipWindow.Bounds.Size;

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

            _tooltipWindow.Position = new PixelPoint((int)x, (int)y);
        }

        private void PickElement(PixelPoint point)
        {
            var maskRect = new Rect();

            switch (_pickMode)
            {
                case PickElementMode.Screen:
                {
                    var screen = Screens.All.FirstOrDefault(s => s.Bounds.Contains(point));
                    if (screen is null) break;

                    // Find corresponding NSScreen
                    // NSScreen.Screens should match order or we can match by frame.
                    // Avalonia Screen Bounds are in global coordinates.
                    // NSScreen Frame is in Cocoa coordinates (bottom-left).
                    // We need to convert point to Cocoa to find NSScreen?
                    // Or just iterate NSScreen and check if it matches Avalonia Screen.
                    // Actually, VisualElementContext.ElementFromPoint(Screen) does this.

                    // Let's use VisualElementContext logic if possible, or reimplement.
                    // VisualElementContext.ElementFromPoint(Screen) uses NSScreenVisualElement.

                    // We can just use the logic from VisualElementContext.ElementFromPoint(Screen)
                    // But we need to pass the point.

                    // Let's try to find the NSScreen that matches the Avalonia Screen.
                    // Avalonia Screen has a Handle? No.
                    // But we can use the point to find the NSScreen.

                    // Convert point to Cocoa coordinates for NSScreen.Screens check?
                    // Actually NSScreen.Screens[0] is (0,0) bottom-left.
                    // Avalonia (0,0) is top-left.
                    // It's easier to just use VisualElementContext.ElementFromPoint logic which takes PixelPoint.

                    _selectedElement = ElementFromPoint(point, PickElementMode.Screen);

                    if (_selectedElement is not null)
                    {
                        // We need the bounds of the screen in Avalonia coordinates.
                        // _selectedElement.BoundingRectangle should return it.
                        maskRect = _selectedElement.BoundingRectangle.Translate(-(PixelVector)_screenBounds.Position).ToRect(1d);
                    }
                    break;
                }
                case PickElementMode.Window:
                {
                    _selectedElement = ElementFromPoint(point, _pickMode);

                    while (_selectedElement is not null && _selectedElement.Type != VisualElementType.TopLevel)
                    {
                        _selectedElement = _selectedElement.Parent;
                    }

                    if (_selectedElement is not null)
                    {
                        maskRect = _selectedElement.BoundingRectangle.Translate(-(PixelVector)_screenBounds.Position).ToRect(1d);
                    }
                    break;
                }
                case PickElementMode.Element:
                {
                    _selectedElement = ElementFromPoint(point, _pickMode);

                    if (_selectedElement is not null)
                    {
                        maskRect = _selectedElement.BoundingRectangle.Translate(-(PixelVector)_screenBounds.Position).ToRect(1d);
                    }
                    break;
                }
            }

            SetMask(maskRect);
            _elementNameTextBlock.Text = GetElementDescription(_selectedElement);
        }

        private void SetMask(Rect rect)
        {
            if (_previousMaskRect == rect) return;

            _maskBorder.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(Bounds), new RectangleGeometry(rect));
            _elementBoundsBorder.Margin = new Thickness(rect.X, rect.Y, 0, 0);
            _elementBoundsBorder.Width = rect.Width;
            _elementBoundsBorder.Height = rect.Height;

            _previousMaskRect = rect;
        }

        private readonly Dictionary<int, string> _processNameCache = new();

        private string? GetElementDescription(IVisualElement? element)
        {
            if (element is null) return LocaleResolver.Common_None;

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
    }
}