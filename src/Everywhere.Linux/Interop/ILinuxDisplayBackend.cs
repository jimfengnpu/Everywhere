using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Everywhere.Interop;

namespace Everywhere.Linux.Interop;

/// <summary>
/// Abstract display backend contract (X11 / Wayland / other).
/// Implementations should be thread-safe for public methods.
/// </summary>
public interface ILinuxDisplayBackend
{
    IVisualElementContext? Context { get; set; }
    /// <summary>Open the display/resources. Return true when successful.</summary>
    bool Open();

    /// <summary>Close display/resources.</summary>
    void Close();

    /// <summary>
    /// Grab a global key. Returns an id (>0) on success, or 0 on failure.
    /// The backend must handle common modifier permutations (Lock/NumLock).
    /// </summary>
    int GrabKey(KeyboardHotkey hotkey, Action handler);

    /// <summary>Ungrab a previously grabbed key by id.</summary>
    void UngrabKey(int id);

    void GrabAll(Action<KeyboardHotkey, bool> hook);

    void UngrabAll();

    /// <summary>Translate a platform keycode to Avalonia Key. Returns Key.None if unknown.</summary>
    Key KeycodeToAvaloniaKey(uint keycode);

    PixelPoint GetPointer();

    IVisualElement? GetFocusedWindowElement();

    IVisualElement GetWindowElementAt(PixelPoint point);

    IVisualElement GetScreenElement();
    
    void SetWindowCornerRadius(Window window, CornerRadius cornerRadius);

    void SetWindowNoFocus(Window window);

    void SetWindowHitTestInvisible(Window window);

    void RegisterFocusChanged(Action handler);
    
    /// <summary>
    /// Capture screen bitmap of window within rect
    /// </summary>
    /// <param name="window"> if null, capture full screen</param>
    /// <param name="rect"></param>
    /// <returns></returns>
    Bitmap Capture(IVisualElement? window, PixelRect rect);
}