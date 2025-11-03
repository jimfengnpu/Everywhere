using Avalonia.Threading;
using Everywhere.Extensions;
using Everywhere.Views;
using Serilog;

namespace Everywhere.Linux.Interop;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Everywhere.Interop;

public partial class LinuxVisualElementContext
{
    private class ElementPicker
    {
        private readonly LinuxVisualElementContext _context;
        private readonly PickElementMode _mode;

        private PixelRect _selectedRect;
        private readonly TaskCompletionSource<IVisualElement?> _taskCompletionSource = new();

        private IVisualElement? _selectedElement;

        private ElementPicker(
            LinuxVisualElementContext context,
            ILinuxDisplayBackend backend,
            PickElementMode mode)
        {
            _context = context;
            _mode = mode;
            _selectedRect = new PixelRect();
            backend.WindowPickerHook(Pick);
        }

        private PixelRect Pick(PixelPoint pixelPoint)
        {
            var selected = _context.ElementFromPoint(pixelPoint, _mode);
            if(selected != _selectedElement)
            {
                _selectedElement = selected;
                Dispatcher.UIThread.Post(() =>
                {
                    _taskCompletionSource.TrySetResult(_selectedElement);
                });
                _selectedRect = selected?.BoundingRectangle?? new PixelRect();
                Log.Logger.Information("{maskRect}",  _selectedRect);
            }

            return _selectedRect;
        }

        public static Task<IVisualElement?> PickAsync(
            LinuxVisualElementContext context,
            ILinuxDisplayBackend backend,
            PickElementMode mode)
        {
            var window = new ElementPicker(context, backend, mode);
            return window._taskCompletionSource.Task;
        }
    }
}