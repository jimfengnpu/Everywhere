using Everywhere.Interop;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace Everywhere.Linux.Interop;

// Wrapper class for LinuxDisplayBackend
public class LinuxDisplayBackend : ILinuxDisplayBackend, IDisposable
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

    public int GrabKey(KeyboardShortcut hotkey, Action handler)
    {
        return _impl.GrabKey(hotkey, handler);
    }

    public void Ungrab(int id)
    {
        _impl.Ungrab(id);
    }

    public void GrabKeyHook(Action<KeyboardShortcut, EventType> hook)
    {
        _impl.GrabKeyHook(hook);
    }

    public void UngrabKeyHook()
    {
        _impl.UngrabKeyHook();
    }

    public void GrabMouse(MouseShortcut hotkey, Action handler)
    {
        _impl.GrabMouse(hotkey, handler);
    }

    public void UngrabMouse(int id)
    {
        _impl.UngrabMouse(id);
    }

    public void GrabMouseHook(Action<PixelPoint, EventType> hook)
    {
        _impl.GrabMouseHook(hook);
    }

    public void UngrabMouseHook()
    {
        _impl.UngrabMouseHook();
    }

    public Key KeycodeToAvaloniaKey(uint keycode)
    {
        return _impl.KeycodeToAvaloniaKey(keycode);
    }

    public PixelPoint GetPointer()
    {
        return _impl.GetPointer();
    }

    public void WindowPickerHook(Func<PixelPoint, PixelRect> hook)
    {
        _impl.WindowPickerHook(hook);
    }

    public IVisualElement? GetFocusedWindowElement()
    {
        return _impl.GetFocusedWindowElement();
    }

    public IVisualElement GetWindowElementAt(PixelPoint point)
    {
        return _impl.GetWindowElementAt(point);
    }

    public IVisualElement? GetWindowElementByPid(int pid)
    {
        return _impl.GetWindowElementByPid(pid);
    }

    public IVisualElement GetScreenElement()
    {
        return _impl.GetScreenElement();
    }

    public void SetFocusable(Window window, bool focusable)
    {
        _impl.SetFocusable(window, focusable);
    }

    public void SetHitTestVisible(Window window, bool visible)
    {
        _impl.SetHitTestVisible(window, false);
    }

    public bool GetEffectiveVisible(Window window)
    {
        return _impl.GetEffectiveVisible(window);
    }

    public void SetCloaked(Window window, bool cloaked)
    {
        _impl.SetCloaked(window, cloaked);
    }

    public bool AnyModelDialogOpened(Window window)
    {
        return _impl.AnyModelDialogOpened(window);
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