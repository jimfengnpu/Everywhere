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
            ElementPickMode mode)
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
        private readonly ElementPickMode _elementPickMode;
        private readonly PixelRect _allScreenBounds;
        private readonly VisualElementPickerMaskWindow[] _visualElementPickerMaskWindows;
        private readonly VisualElementPickerToolTipWindow _toolTipWindow;
        private IVisualElement? _selectedElement;

        private ElementPicker(
            LinuxVisualElementContext context,
            ILinuxWindowBackend backend,
            ElementPickMode elementPickMode)
        {
            _context = context;
            _elementPickMode = elementPickMode;
            var allScreens = Screens.All;
            _visualElementPickerMaskWindows = new VisualElementPickerMaskWindow[allScreens.Count];
            for (var i = 0; i < allScreens.Count; i++)
            {
                var screen = allScreens[i];
                _allScreenBounds = _allScreenBounds.Union(screen.Bounds);
                var visualElementPickerMaskWindow = new VisualElementPickerMaskWindow(screen.Bounds);
                backend.SetHitTestVisible(visualElementPickerMaskWindow, false);
                _visualElementPickerMaskWindows[i] = visualElementPickerMaskWindow;
            }

            SetWindowPlacement(this, _allScreenBounds, out _);
            _toolTipWindow = new VisualElementPickerToolTipWindow(_elementPickMode);

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

            _context._eventHelper.WindowPickerHook(
                this,
                (point, type) =>
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
                            Dispatcher.UIThread.Post(
                                () =>
                                {
                                    if (leftPressed)
                                    {
                                        PickElement(point);
                                    }
                                    SetToolTipWindowPosition(point);
                                },
                                DispatcherPriority.Default);
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
            _selectedElement = _context.ElementFromPoint(pixelPoint, _elementPickMode);
            if (_selectedElement == null) return;
            var maskRect = _selectedElement.BoundingRectangle;
            foreach (var maskWindow in _visualElementPickerMaskWindows) maskWindow.SetMask(maskRect);
            _toolTipWindow.ToolTip.Element = _selectedElement;
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
    }
}