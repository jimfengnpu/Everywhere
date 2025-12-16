namespace Everywhere.Interop;

public enum ElementPickMode
{
    /// <summary>
    /// Pick a whole screen.
    /// </summary>
    Screen,

    /// <summary>
    /// Pick a window.
    /// </summary>
    Window,

    /// <summary>
    /// Pick a specific element.
    /// </summary>
    Element
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
    IVisualElement? ElementFromPoint(PixelPoint point, ElementPickMode mode = ElementPickMode.Element);

    /// <summary>
    /// Get the element under the mouse pointer.
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    IVisualElement? ElementFromPointer(ElementPickMode mode = ElementPickMode.Element);

    /// <summary>
    /// Let the user pick an element from the screen.
    /// </summary>
    /// <returns></returns>
    Task<IVisualElement?> PickElementAsync(ElementPickMode mode);
}