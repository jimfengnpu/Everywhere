using Avalonia.Threading;

namespace Everywhere.Linux.Interop;

using Avalonia;
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
            // backend.WindowPickerHook(Pick);
            // TODO: element picker
            throw new NotImplementedException();
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