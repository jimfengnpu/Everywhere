using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Everywhere.Extensions;
using Everywhere.I18N;
using Everywhere.Interop;
using Everywhere.Views;
using Point = System.Drawing.Point;
using Window = Avalonia.Controls.Window;

namespace Everywhere.Windows.Interop;

/// <summary>
/// A utility class for picking visual elements from the screen.
/// </summary>
public partial class VisualElementContext
{
    /// <summary>
    /// A window that allows the user to pick an element from the screen.
    /// </summary>
    private class VisualElementPicker : Window
    {
        public static Task<IVisualElement?> PickAsync(IWindowHelper windowHelper, PickElementMode mode)
        {
            var window = new VisualElementPicker(windowHelper, mode);
            window.Show();
            return window._pickingPromise.Task;
        }

        /// <summary>
        /// A promise that resolves to the picked visual element.
        /// </summary>
        private readonly TaskCompletionSource<IVisualElement?> _pickingPromise = new();

        private readonly IWindowHelper _windowHelper;

        private readonly PixelRect _screenBounds;
        private readonly Border _maskBorder;
        private readonly Border _elementBoundsBorder;
        private readonly double _scale;

        private readonly ElementPickerToolTipWindow _tooltipWindow;

        private PickElementMode _pickMode;
        private Rect? _previousMaskRect;
        private IVisualElement? _selectedElement;

        private bool _isRightButtonPressed;
        private LowLevelMouseHook? _mouseHook;
        private LowLevelKeyboardHook? _keyboardHook;

        private VisualElementPicker(IWindowHelper windowHelper, PickElementMode pickMode)
        {
            _windowHelper = windowHelper;
            _pickMode = pickMode;

            var allScreens = Screens.All;
            _screenBounds = allScreens.Aggregate(default(PixelRect), (current, screen) => current.Union(screen.Bounds));
            if (_screenBounds.Width <= 0 || _screenBounds.Height <= 0)
            {
                throw new InvalidOperationException("No valid screen bounds found.");
            }

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

            Topmost = true;
            CanResize = false;
            ShowInTaskbar = false;
            SystemDecorations = SystemDecorations.None;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Background = Brushes.Transparent;
            Cursor = new Cursor(StandardCursorType.Cross);
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

            Position = _screenBounds.Position;
            _scale = DesktopScaling; // we must set Position first to get the correct scaling factor
            Width = _screenBounds.Width / _scale;
            Height = _screenBounds.Height / _scale;

            _tooltipWindow = new ElementPickerToolTipWindow();
            windowHelper.SetHitTestVisible(_tooltipWindow, false);
        }

        protected override unsafe void OnPointerEntered(PointerEventArgs e)
        {
            // Simulate a mouse left button down in the top-left corner of the window (8,8 to avoid the border)
            var x = (_screenBounds.X + 8d) / _screenBounds.Width * 65535;
            var y = (_screenBounds.Y + 8d) / _screenBounds.Height * 65535;

            // SendInput MouseRightButtonDown, this will:
            // 1. prevent the cursor from changing to the default arrow cursor and interacting with other windows (behaviors like Spy++ etc.)
            // 2. Trigger the OnPointerPressed event to set the window to hit test invisible
            PInvoke.SendInput(
                new ReadOnlySpan<INPUT>(
                [
                    new INPUT
                    {
                        type = INPUT_TYPE.INPUT_MOUSE,
                        Anonymous = new INPUT._Anonymous_e__Union
                        {
                            mi = new MOUSEINPUT
                            {
                                dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE,
                                dx = (int)x,
                                dy = (int)y
                            }
                        }
                    },
                ]),
                sizeof(INPUT));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            // This should be triggered by the SendInput above
            if (_isRightButtonPressed || !e.Properties.IsRightButtonPressed) return;

            _isRightButtonPressed = true;
            _windowHelper.SetHitTestVisible(this, false);
            _tooltipWindow.Show(this);

            // Install a low-level mouse hook to listen for right button down events
            // This is needed because once we set the window to hit test invisible
            _mouseHook ??= new LowLevelMouseHook((msg, ref hookStruct, ref blockNext) =>
            {
                switch (msg)
                {
                    // Close the window and cancel selection on right button up (Exit the picking mode)
                    case WINDOW_MESSAGE.WM_RBUTTONUP:
                    {
                        blockNext = true;

                        // exit picking mode without selecting an element
                        _selectedElement = null;
                        Dispatcher.UIThread.Post(Close, DispatcherPriority.Default);
                        break;
                    }
                    case WINDOW_MESSAGE.WM_LBUTTONUP:
                    {
                        blockNext = true;

                        Dispatcher.UIThread.Post(Close, DispatcherPriority.Default);
                        break;
                    }
                    // Use scroll wheel to change pick mode
                    case WINDOW_MESSAGE.WM_MOUSEWHEEL:
                    {
                        blockNext = true;

                        var delta = (int)hookStruct.mouseData >> 16;
                        _pickMode = (PickElementMode)((int)(_pickMode + (delta > 0 ? -1 : 1)) switch
                        {
                            > 2 => 0,
                            < 0 => 2,
                            var v => v
                        });
                        HandlePickModeChanged();
                        break;
                    }
                    case WINDOW_MESSAGE.WM_MOUSEMOVE:
                    {
                        break; // allow mouse move events
                    }
                    default:
                    {
                        blockNext = true; // block all other mouse events
                        break;
                    }
                }
            });

            _keyboardHook ??= new LowLevelKeyboardHook((msg, ref hookStruct, ref blockNext) =>
            {
                // Block all key events
                blockNext = true;

                var isKeyDown = msg is WINDOW_MESSAGE.WM_KEYDOWN or WINDOW_MESSAGE.WM_SYSKEYDOWN;
                if (!isKeyDown) return;

                switch ((VIRTUAL_KEY)hookStruct.vkCode)
                {
                    case VIRTUAL_KEY.VK_ESCAPE:
                    {
                        _selectedElement = null;

                        // Close on next UI thread loop, so that current event can be blocked
                        Dispatcher.UIThread.Post(Close, DispatcherPriority.Default);
                        break;
                    }
                    case VIRTUAL_KEY.VK_NUMPAD1 or VIRTUAL_KEY.VK_1 or VIRTUAL_KEY.VK_F1:
                    {
                        _pickMode = PickElementMode.Screen;
                        HandlePickModeChanged();
                        break;
                    }
                    case VIRTUAL_KEY.VK_NUMPAD2 or VIRTUAL_KEY.VK_2 or VIRTUAL_KEY.VK_F2:
                    {
                        _pickMode = PickElementMode.Window;
                        HandlePickModeChanged();
                        break;
                    }
                    case VIRTUAL_KEY.VK_NUMPAD3 or VIRTUAL_KEY.VK_3 or VIRTUAL_KEY.VK_F3:
                    {
                        _pickMode = PickElementMode.Element;
                        HandlePickModeChanged();
                        break;
                    }
                }
            });

            // Pick the element under the cursor immediately
            HandlePointerMoved();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            HandlePointerMoved();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            Close();
        }

        protected override unsafe void OnClosed(EventArgs e)
        {
            _mouseHook?.Dispose();
            _keyboardHook?.Dispose();

            // right button down event (from SendInput) is not blocked and triggered OnPointerPressed
            // so currently system thinks right button is still pressed
            // we need to let the right button up event go through to avoid stuck right button state
            PInvoke.SendInput(
                new ReadOnlySpan<INPUT>(
                [
                    new INPUT
                    {
                        type = INPUT_TYPE.INPUT_MOUSE,
                        Anonymous = new INPUT._Anonymous_e__Union
                        {
                            mi = new MOUSEINPUT
                            {
                                dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE,
                            }
                        }
                    },
                ]),
                sizeof(INPUT));

            _pickingPromise.TrySetResult(_selectedElement);
        }

        private void HandlePickModeChanged()
        {
            HandlePointerMoved();
            Dispatcher.UIThread.Post(() =>
            {
                _tooltipWindow.Mode = _pickMode;
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// Handle pointer moved event to update the picked element and tooltip position.
        /// </summary>
        private void HandlePointerMoved()
        {
            if (PInvoke.GetCursorPos(out var point)) PickElement(point);
            SetToolTipWindowPosition(new PixelPoint(point.X, point.Y));
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
            var tooltipSize = _tooltipWindow.Bounds.Size * _scale;

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

        private void PickElement(Point point)
        {
            var maskRect = new Rect();
            var pixelPoint = new PixelPoint(point.X, point.Y);
            switch (_pickMode)
            {
                case PickElementMode.Screen:
                {
                    var screen = Screens.All.FirstOrDefault(s => s.Bounds.Contains(pixelPoint));
                    if (screen == null) break;

                    var hMonitor = PInvoke.MonitorFromPoint(point, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                    if (hMonitor == HMONITOR.Null) break;

                    _selectedElement = new ScreenVisualElementImpl(hMonitor);

                    maskRect = screen.Bounds.Translate(-(PixelVector)_screenBounds.Position).ToRect(_scale);
                    break;
                }
                case PickElementMode.Window:
                {
                    var selectedHWnd = PInvoke.WindowFromPoint(point);
                    if (selectedHWnd == HWND.Null) break;

                    var rootHWnd = PInvoke.GetAncestor(selectedHWnd, GET_ANCESTOR_FLAGS.GA_ROOTOWNER);
                    if (rootHWnd == HWND.Null) break;

                    _selectedElement = TryCreateVisualElement(() => Automation.FromHandle(rootHWnd));
                    if (_selectedElement == null) break;

                    maskRect = _selectedElement.BoundingRectangle.Translate(-(PixelVector)_screenBounds.Position).ToRect(_scale);
                    break;
                }
                case PickElementMode.Element:
                {
                    // TODO: sometimes this only picks the window, not the element under the cursor?
                    _selectedElement = TryCreateVisualElement(() => Automation.FromPoint(point));
                    if (_selectedElement == null) break;

                    maskRect = _selectedElement.BoundingRectangle.Translate(-(PixelVector)_screenBounds.Position).ToRect(_scale);
                    break;
                }
            }

            SetMask(maskRect);
            _tooltipWindow.Header = GetElementDescription(_selectedElement);
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