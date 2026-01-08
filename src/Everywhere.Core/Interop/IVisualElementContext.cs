using Avalonia.Media.Imaging;

namespace Everywhere.Interop;

public enum ScreenSelectionMode
{
    /// <summary>
    /// Pick a whole screen.
    /// </summary>
    [DynamicResourceKey(LocaleKey.ScreenSelectionMode_Screen)]
    Screen,

    /// <summary>
    /// Pick a window.
    /// </summary>
    [DynamicResourceKey(LocaleKey.ScreenSelectionMode_Window)]
    Window,

    /// <summary>
    /// Pick a specific element.
    /// </summary>
    [DynamicResourceKey(LocaleKey.ScreenSelectionMode_Element)]
    Element,

    /// <summary>
    /// Free selection mode.
    /// </summary>
    [DynamicResourceKey(LocaleKey.ScreenSelectionMode_Free)]
    Free
}

public interface IVisualElementContext
{
    IVisualElement? KeyboardFocusedElement { get; }

    /// <summary>
    /// Get the element at the specified point.
    /// </summary>
    /// <param name="point">Point in screen pixels.</param>
    /// <param name="mode"></param>
    /// <returns></returns>
    IVisualElement? ElementFromPoint(PixelPoint point, ScreenSelectionMode mode = ScreenSelectionMode.Element);

    /// <summary>
    /// Get the element under the mouse pointer.
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    IVisualElement? ElementFromPointer(ScreenSelectionMode mode = ScreenSelectionMode.Element);

    /// <summary>
    /// Let the user pick an element from the screen.
    /// </summary>
    /// <param name="initialMode">
    /// The initial pick mode to use. If null, it remembers the last used mode.
    /// </param>
    /// <returns></returns>
    Task<IVisualElement?> PickElementAsync(ScreenSelectionMode? initialMode);

    /// <summary>
    /// Let the user take a screenshot of a selected area.
    /// </summary>
    /// <param name="initialMode">
    /// The initial pick mode to use. If null, it remembers the last used mode.
    /// </param>
    /// <returns></returns>
    Task<Bitmap?> ScreenshotAsync(ScreenSelectionMode? initialMode);
}