using Everywhere.Interop;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace Everywhere.Linux.Interop;

// Wrapper class for LinuxDisplayBackend
public class LinuxDisplayBackend : ILinuxDisplayBackend
{
    private readonly ILinuxDisplayBackend _impl;
    public IVisualElementContext? Context { get; set; }

    public LinuxDisplayBackend()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            throw new PlatformNotSupportedException("LinuxDisplayBackend can only be used on Linux.");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
            throw new InvalidOperationException("DISPLAY environment variable is not set. DisplayBackend cannot be initialized.");
        // Detect by environment variable XDG_SESSION_TYPE
        var isX11 = string.Equals(
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            "x11", StringComparison.OrdinalIgnoreCase);
        if (isX11)
        {
            _impl = new X11DisplayBackend();
        }
        else
        {
            throw new PlatformNotSupportedException("Other Backend(Such as Wayland) is not supported yet.");
        }
        _impl.Open();
    }
    public void Close()
    {
        _impl.Close();
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    public bool Open()
    {
        return _impl.Open();
    }

    public int GrabKey(KeyboardHotkey hotkey, Action handler)
    {
        return _impl.GrabKey(hotkey, handler);
    }

    public void UngrabKey(int id)
    {
        _impl.UngrabKey(id);
    }

    public void GrabAll(Action<KeyboardHotkey, bool> hook)
    {
        _impl.GrabAll(hook);
    }

    public void UngrabAll()
    {
        _impl.UngrabAll();
    }
    
    public Key KeycodeToAvaloniaKey(uint keycode)
    {
        return _impl.KeycodeToAvaloniaKey(keycode);
    }

    public PixelPoint GetPointer()
    {
        return _impl.GetPointer();
    }

    public IVisualElement? GetFocusedWindowElement()
    {
        return _impl.GetFocusedWindowElement();
    }

    public IVisualElement GetWindowElementAt(PixelPoint point)
    {
        return _impl.GetWindowElementAt(point);
    }

    public IVisualElement GetScreenElement()
    {
        return _impl.GetScreenElement();
    }

    public void SetWindowCornerRadius(Window window, CornerRadius cornerRadius)
    {
        _impl.SetWindowCornerRadius(window, cornerRadius);
    }

    public void SetWindowNoFocus(Window window)
    {
        _impl.SetWindowNoFocus(window);
    }

    public void SetWindowHitTestInvisible(Window window)
    {
        _impl.SetWindowHitTestInvisible(window);
    }

    public void RegisterFocusChanged(Action handler)
    {
        _impl.RegisterFocusChanged(handler);
    }

    public Bitmap Capture(IVisualElement? window, PixelRect rect)
    {
        return _impl.Capture(window, rect);
    }
}