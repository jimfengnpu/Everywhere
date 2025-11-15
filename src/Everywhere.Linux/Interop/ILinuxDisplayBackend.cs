using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Everywhere.Interop;

namespace Everywhere.Linux.Interop;


public enum EventType
{
    Unknown,
    KeyDown,
    KeyUp,
    MouseDown,
    MouseUp,
    MouseRight,
    MouseMiddle,
    MouseDrag,
    MouseWheelDown,
    MouseWheelUp,
    MouseWheelLeft,
    MouseWheelRight,
    FocusChange,
}

public readonly record struct PointerState(PixelPoint Point, int ButtonMask);

/// <summary>
/// Abstract display _backend contract (X11 / Wayland / other).
/// Implementations should be thread-safe for public methods.
/// </summary>
public interface ILinuxDisplayBackend: IWindowHelper
{
    IVisualElementContext? Context { get; set; }
    /// <summary>Open the display/resources. Return true when successful.</summary>
    bool Open();

    /// <summary>Close display/resources.</summary>
    void Close();

    /// <summary>
    /// Grab a global key. Returns an id (>0) on success, or 0 on failure.
    /// The _backend must handle common modifier permutations (Lock/NumLock).
    /// </summary>
    int GrabKey(KeyboardShortcut hotkey, Action handler);

    /// <summary>Ungrab a previously grabbed key by id.</summary>
    void Ungrab(int id);

    void GrabKeyHook(Action<KeyboardShortcut, EventType> hook);

    void UngrabKeyHook();
    
    void GrabMouse(MouseShortcut hotkey, Action handler);
    
    void UngrabMouse(int id);
    
    void GrabMouseHook(Action<PixelPoint, EventType> hook);
    
    void UngrabMouseHook();

    /// <summary>Translate a platform keycode to Avalonia Key. Returns Key.None if unknown.</summary>
    Key KeycodeToAvaloniaKey(uint keycode);

    PixelPoint GetPointer();
    
    // void WindowPickerHook(Func<PixelPoint, PixelRect> hook);

    IVisualElement? GetFocusedWindowElement();

    IVisualElement GetWindowElementAt(PixelPoint point);

    IVisualElement? GetWindowElementByPid(int pid);

    IVisualElement GetScreenElement();

    void RegisterFocusChanged(Action handler);
    
    /// <summary>
    /// Capture screen bitmap of window within rect
    /// </summary>
    /// <param name="window">capture target</param>
    /// <param name="rect"> used for capturing full screen</param>
    /// <returns></returns>
    Bitmap Capture(IVisualElement? window, PixelRect rect);
}