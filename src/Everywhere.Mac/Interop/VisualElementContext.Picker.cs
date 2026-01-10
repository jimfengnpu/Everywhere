using Everywhere.Interop;

namespace Everywhere.Mac.Interop;

public partial class VisualElementContext
{
    private class PickerSession : ScreenSelectionSession
    {
        public static Task<IVisualElement?> PickAsync(IWindowHelper windowHelper, ScreenSelectionMode mode)
        {
            var window = new PickerSession(windowHelper, mode);
            window.Show();
            return window._pickingPromise.Task;
        }

        private readonly TaskCompletionSource<IVisualElement?> _pickingPromise = new();

        private PickerSession(IWindowHelper windowHelper, ScreenSelectionMode screenSelectionMode)
            : base(
                windowHelper,
                [ScreenSelectionMode.Screen, ScreenSelectionMode.Window, ScreenSelectionMode.Element],
                screenSelectionMode)
        {
        }

        protected override void OnClosed(EventArgs e)
        {
            _pickingPromise.TrySetResult(SelectedElement);
            base.OnClosed(e);
        }
    }
}