using Everywhere.Interop;

namespace Everywhere.Windows.Interop;

/// <summary>
/// A utility class for picking visual elements from the screen.
/// </summary>
public partial class VisualElementContext
{
    /// <summary>
    /// A window that allows the user to pick an element from the screen.
    /// </summary>
    private sealed class PickerSession : ScreenSelectionSession
    {
        private static ScreenSelectionMode _previousMode = ScreenSelectionMode.Element;

        public static Task<IVisualElement?> PickAsync(IWindowHelper windowHelper, ScreenSelectionMode? initialMode)
        {
            var window = new PickerSession(windowHelper, initialMode ?? _previousMode);
            window.Show();
            return window._pickingPromise.Task;
        }

        /// <summary>
        /// A promise that resolves to the picked visual element.
        /// </summary>
        private readonly TaskCompletionSource<IVisualElement?> _pickingPromise = new();

        private PickerSession(IWindowHelper windowHelper, ScreenSelectionMode initialMode)
            : base(windowHelper, [ScreenSelectionMode.Screen, ScreenSelectionMode.Window, ScreenSelectionMode.Element], initialMode)
        {
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _previousMode = CurrentMode;
            _pickingPromise.TrySetResult(SelectedElement);
        }
    }
}