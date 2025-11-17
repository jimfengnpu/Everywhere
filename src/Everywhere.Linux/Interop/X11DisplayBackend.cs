using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Everywhere.Common;
using Everywhere.Interop;
using Microsoft.Extensions.Logging;

namespace Everywhere.Linux.Interop;


public sealed partial class X11DisplayBackend : ILinuxDisplayBackend
{
    private IntPtr _display;
    private IntPtr _rootWindow;
    private IntPtr _scanSkipWindowHandle = IntPtr.Zero;
    private readonly ILogger<X11DisplayBackend> _logger = ServiceLocator.Resolve<ILogger<X11DisplayBackend>>();
    private readonly ConcurrentDictionary<int, RegInfo> _regs = new();
    private int _nextId = 1;
    private readonly BlockingCollection<Action> _ops = new(new ConcurrentQueue<Action>());
    private Thread? _xThread;
    private volatile bool _running;
    private Action<KeyboardShortcut, EventType>? _keyboardHook;
    private Action<PixelPoint, EventType>? _mouseHook;
    private Action? _focusChangedHook;
    private int _wakePipeR = -1;
    private int _wakePipeW = -1;
    private readonly ConcurrentDictionary<IntPtr, IVisualElement> _windowCache = new();

    public bool IsAvailable => _display != IntPtr.Zero;
    public IVisualElementContext? Context { get; set; }

    public bool Open()
    {

        XInitThreads();
        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero) return false;
        _rootWindow = XDefaultRootWindow(_display);
        // select key events on root window so we receive KeyPress/KeyRelease
        XSelectInput(_display, _rootWindow,
            KeyPressMask | KeyReleaseMask | ButtonPressMask | ButtonReleaseMask | ButtonMotionMask | FocusChangeMask);
        // create wake pipe
        try
        {
            var fds = new int[2];
            if (pipe(fds) == 0)
            {
                _wakePipeR = fds[0];
                _wakePipeW = fds[1];
                try
                {
                    // set non-blocking
                    var flags = fcntl(_wakePipeR, F_GETFL, 0);
                    fcntl(_wakePipeR, F_SETFL, flags | O_NONBLOCK);
                    fcntl(_wakePipeW, F_SETFL, flags | O_NONBLOCK);
                    // set close-on-exec
                    fcntl(_wakePipeR, F_SETFD, FD_CLOEXEC);
                    fcntl(_wakePipeW, F_SETFD, FD_CLOEXEC);
                }
                catch (Exception ex)
                {
                    _logger.LogError("XThread fcntl Error: {ex}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("XThread pipe file Error: {ex}", ex.Message);
        }

        _running = true;
        _xThread = new Thread(XThreadMain) { IsBackground = true, Name = "X11DisplayThread" };
        _xThread.Start();
        return true;
    }

    public void Close()
    {
        _running = false;
        try
        {
            _ops.CompleteAdding();
            // wake the thread so poll/select returns
            if (_wakePipeW != -1)
            {
                var b = new byte[] { 1 };
                write(_wakePipeW, b, 1);
            }
            if (_xThread?.IsAlive == true) _xThread.Join(500);
            if (_display != IntPtr.Zero)
            {
                XCloseDisplay(_display);
                _display = IntPtr.Zero;
            }
            if (_wakePipeR != -1) close(_wakePipeR);
            if (_wakePipeW != -1) close(_wakePipeW);
        }
        catch (Exception ex)
        {
            _logger.LogError("X11 Backend Close Error: {ex}", ex.Message);
        }
    }

    public int GrabKey(KeyboardShortcut hotkey, Action handler)
    {
        if (_display == IntPtr.Zero) return 0;
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        XThreadAction(() =>
        {
            try
            {
                uint mods = 0;
                if (hotkey.Modifiers.HasFlag(KeyModifiers.Shift)) mods |= ShiftMask;
                if (hotkey.Modifiers.HasFlag(KeyModifiers.Control)) mods |= ControlMask;
                if (hotkey.Modifiers.HasFlag(KeyModifiers.Alt)) mods |= Mod1Mask;
                if (hotkey.Modifiers.HasFlag(KeyModifiers.Meta)) mods |= Mod4Mask;

                var ks = XStringToKeysym(hotkey.Key.ToString());
                int keycode = 0;
                if (ks != UIntPtr.Zero) keycode = XKeysymToKeycode(_display, ks);
                if (keycode == 0)
                {
                    ks = XStringToKeysym(hotkey.Key.ToString().ToUpperInvariant());
                    if (ks != UIntPtr.Zero) keycode = XKeysymToKeycode(_display, ks);
                }
                if (keycode == 0)
                {
                    tcs.SetResult(0);
                    return;
                }

                var variants = new uint[] { 0u, LockMask, Mod2Mask, LockMask | Mod2Mask };
                foreach (var v in variants) XGrabKey(_display, keycode, mods | v, _rootWindow, 0, GrabModeAsync, GrabModeAsync);
                XFlush(_display);
                var id = Interlocked.Increment(ref _nextId);
                _regs[id] = new RegInfo { Keycode = keycode, Mods = mods, Handler = handler };
                tcs.SetResult(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GrabKey op failed");
                tcs.SetResult(0);
            }
        });

        return tcs.Task.GetAwaiter().GetResult();
    }

    public void Ungrab(int id)
    {
        if (_display == IntPtr.Zero) return;
        XThreadAction(() =>
        {
            if (!_regs.TryRemove(id, out var info)) return;
            var variants = new uint[] { 0u, LockMask, Mod2Mask, LockMask | Mod2Mask };
            foreach (var v in variants) XUngrabKey(_display, info.Keycode, info.Mods | v, _rootWindow);
            XFlush(_display);

        });
    }

    public void GrabKeyHook(Action<KeyboardShortcut, EventType> hook)
    {
        _keyboardHook = hook;
        XThreadAction(() =>
        {
            XGrabKeyboard(_display, _rootWindow,
                0, GrabModeAsync, GrabModeAsync, CurrentTime);
            XFlush(_display);
        });
    }

    public void UngrabKeyHook()
    {
        _keyboardHook = null;
        XThreadAction(() =>
        {
            XUngrabKeyboard(_display, _rootWindow);
            // In X11 Ungrab do not remove focus, set focus to root
            XSetInputFocus(_display, _rootWindow, RevertToParent, CurrentTime);
            XFlush(_display);
        });
    }

    public void GrabMouse(MouseShortcut hotkey, Action handler)
    {
        throw new NotImplementedException();
    }

    public void UngrabMouse(int id)
    {
        throw new NotImplementedException();
    }

    public void GrabMouseHook(Action<PixelPoint, EventType> hook)
    {
        _mouseHook = hook;
        XThreadAction(() =>
        {
            XGrabPointer(_display, _rootWindow,
                0, ButtonPressMask | ButtonReleaseMask | ButtonMotionMask,
                GrabModeAsync, GrabModeAsync, IntPtr.Zero, IntPtr.Zero, CurrentTime);
            XFlush(_display);
        });
    }

    public void UngrabMouseHook()
    {
        _mouseHook = null;
        XThreadAction(() =>
        {
            XUngrabPointer(_display, CurrentTime);
            XFlush(_display);
        });
    }

    public Key KeycodeToAvaloniaKey(uint keycode)
    {
        try
        {
            var ks = XKeycodeToKeysym(_display, (int)keycode, 0);
            var name = KeysymToString(ks);
            if (!string.IsNullOrEmpty(name) && name.Length == 1 && char.IsLetter(name[0])) return (Key)Enum.Parse(typeof(Key), name.ToUpperInvariant());
        }
        catch { }
        return Key.None;
    }

    private void XGetProperty(
        IntPtr window,
        string propertyName,
        long length,
        IntPtr reqType,
        Action<IntPtr, int, ulong, ulong, IntPtr> propertyCallback)
    {// actualType, actualFormat, nitems, bytesAfter, data
        IntPtr atom = XInternAtom(_display, propertyName, 1);
        if (atom == IntPtr.Zero) return;

        XGetWindowProperty(_display, window, atom,
            0, length, 0, reqType,
            out var actualType, out var actualFormat,
            out var nItems, out var bytesAfter, out var prop);
        try
        {
            propertyCallback(actualType, actualFormat, nItems, bytesAfter, prop);
        }
        finally
        {
            XFree(prop);
        }
    }

    private int XGetWindowPid(IntPtr window)
    {
        int pid = 0;
        XGetProperty(window, "_NET_WM_PID", 1, 
            (IntPtr)XA_CARDINAL, (_, _, nItems, _, prop) =>
        {
            if (nItems == 0 || prop == IntPtr.Zero)
                return;
            pid = Marshal.ReadInt32(prop);
        });
        return pid;
    }
    
    private void XForEachTopWindow(Action<IntPtr> handle)
    {
        XGetProperty(_rootWindow, "_NET_CLIENT_LIST", -1
            , XA_WINDOW,
            (type, format, count, _, data) =>
            {
                if (type == XA_WINDOW && format == 32)
                {
                    for (var i = 0; i < ((int)count); i++)
                    {
                        var client = 
                            Marshal.ReadIntPtr(data, i * IntPtr.Size);
                        if (client != IntPtr.Zero)
                        {
                            handle(client);
                        }
                    }
                }
            });
    }

    private IVisualElement GetWindowElement(IntPtr window, Func<IVisualElement> maker)
    {
        // return cached if any
        if (_windowCache.TryGetValue(window, out var cachedElement))
        {
            _logger.LogDebug("Using cached window element for {window}", window.ToString("X"));
            return cachedElement;
        }

        // create
        var element = maker();
        _logger.LogInformation("Creating window element for {window}", window.ToString("X"));
        _windowCache[window] = element;
        return element;
    }
    
    private IVisualElement GetWindowElement(IntPtr window)
    {
        return GetWindowElement(window, () => new X11WindowVisualElement(this, window));
    }

    public IVisualElement? GetFocusedWindowElement()
    {
        try
        {
            if (_display == IntPtr.Zero) return null;
            XGetInputFocus(_display, out var focusWindow, out _);
            if (focusWindow == IntPtr.Zero) return null;
            return GetWindowElement(focusWindow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetFocusedWindowElement failed");
            return null;
        }
    }

    private IntPtr GetWindowAtPoint(IntPtr window, int x, int y)
    {
        if (window == _scanSkipWindowHandle)
        {
            return IntPtr.MaxValue;
        }
        XGetWindowAttributes(_display, window, out var attr);
        if (attr.map_state != IsViewable || attr.override_redirect == 1)// skip hidden
        {
            return IntPtr.Zero;
        }
        var rx = x - attr.x;
        var ry = y - attr.y;
        XQueryTree(_display, window, out _, out _, out var childrenPtr, out var count);
        for (var i = count - 1; i >= 0; i--) // backward iter to find the topmost
        {
            var child = Marshal.ReadIntPtr(childrenPtr, i * IntPtr.Size);
            if (child != IntPtr.Zero)
            {
                var sub = GetWindowAtPoint(child, rx, ry);
                if (sub == IntPtr.MaxValue)
                {
                    if (window != _rootWindow)
                    {
                        return IntPtr.MaxValue;
                    }
                    continue;
                }
                if (sub != IntPtr.Zero)
                {
                    _logger.LogInformation("< {window}", 
                        window.ToString("X"));
                    return sub;
                }
            }
        }
        if (x < attr.x || y < attr.y ||
            x >= attr.x + attr.width || y >= attr.y + attr.height)
        {
            return IntPtr.Zero;
        }
        _logger.LogInformation("<< get {window}", 
            window.ToString("X"));
        return window;
    }

    public IVisualElement GetWindowElementAt(PixelPoint point)
    {
        var child = GetWindowAtPoint(_rootWindow, point.X, point.Y);
        // if child = 0 fallback to root
        if (child == IntPtr.Zero)
        {
            _logger.LogDebug("XQueryPointer returned child=0, using root window");
            child = _rootWindow;
        }

        _logger.LogInformation("GetWindowElementAt at ({x},{y}) -> window {window}",
            point.X, point.Y, child.ToString("X"));

        return GetWindowElement(child);
    }
    
    public IVisualElement? GetWindowElementByPid(int pid)
    {
        IntPtr target = IntPtr.Zero;
        XForEachTopWindow((window) =>
        {
            if (XGetWindowPid(window) == pid)
            {
                target = window;
            }
        });
        if (target != IntPtr.Zero)
        {
            return GetWindowElement(target);
        }
        return null;
    }

    public IVisualElement GetScreenElement()
    {
        var screenIdx = XDefaultScreen(_display);
        var screenWindow = XRootWindow(_display, screenIdx);
        return GetWindowElement(screenWindow, () => new X11ScreenVisualElement(this, screenIdx));
    }

    // public void SetWindowCornerRadius(Window window, CornerRadius cornerRadius)
    // {
    //     try
    //     {
    //         var ph = window.TryGetPlatformHandle();
    //         if (ph is null) return;
    //         IntPtr wnd = ph.Handle;
    //
    //         if (_display == IntPtr.Zero) return;
    //
    //         XGetGeometry(_display, wnd, out _, out _, out _, out var width, out var height, out _, out _);
    //         if (width == 0 || height == 0) return;
    //
    //         if (XShapeQueryExtension(_display, out _, out _) == 0)
    //         {
    //             _logger.LogWarning("XShape extension not available, cannot set window corner radius");
    //             return;
    //         }
    //
    //         IntPtr mask = XCreatePixmap(_display, wnd, width, height, 1);
    //         IntPtr gc = XCreateGC(_display, mask, 0, IntPtr.Zero);
    //
    //         XSetForeground(_display, gc, 0);
    //         XFillRectangle(_display, mask, gc, 0, 0, width, height);
    //
    //         XSetForeground(_display, gc, 1);
    //         const int Arc90Degrees = 90 * 64;
    //         int r = (int)cornerRadius.TopLeft;
    //         if (r <= 0) r = 8;
    //
    //         // Draw Arc
    //         XFillArc(_display, mask, gc, 0, 0, (uint)(2 * r), (uint)(2 * r),
    //          2 * Arc90Degrees, Arc90Degrees); // Top-left
    //         XFillArc(_display, mask, gc, (int)(width - 2 * r), 0, (uint)(2 * r), (uint)(2 * r),
    //          3 * Arc90Degrees, Arc90Degrees);
    //         XFillArc(_display, mask, gc, 0, (int)(height - 2 * r), (uint)(2 * r), (uint)(2 * r),
    //          Arc90Degrees, Arc90Degrees);
    //         XFillArc(_display, mask, gc, (int)(width - 2 * r), (int)(height - 2 * r), (uint)(2 * r),
    //          (uint)(2 * r), 0, Arc90Degrees);
    //
    //         // Fill Rects
    //         XFillRectangle(_display, mask, gc, r, 0, (uint)(width - 2 * r), height);
    //         XFillRectangle(_display, mask, gc, 0, r, width, (uint)(height - 2 * r));
    //
    //         // Apply the shape mask to the window
    //         XShapeCombineMask(_display, wnd, ShapeBounding, 0, 0, mask, ShapeSet);
    //         XFreeGC(_display, gc);
    //         XFreePixmap(_display, mask);
    //         XFlush(_display);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "X11 SetWindowCornerRadius failed");
    //     }
    // }

    public void SetFocusable(Window window, bool focusable)
    {
        try
        {
            var ph = window.TryGetPlatformHandle();
            if (ph is null) return;
            IntPtr wnd = ph.Handle;

            if (_display == IntPtr.Zero) return;

            // 设置窗口类型为工具窗口，避免在任务栏显示和获得焦点
            IntPtr atomType = XInternAtom(_display, "_NET_WM_WINDOW_TYPE", 0);
            IntPtr atomUtility = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_UTILITY", 0);
            if (atomType != IntPtr.Zero && atomUtility != IntPtr.Zero)
            {
                ulong[] data = { (ulong)atomUtility };
                XChangeProperty(_display, wnd, atomType, XInternAtom(_display, "ATOM", 0), 32, PropModeReplace, data, 1);
            }

            // 跳过任务栏
            IntPtr atomState = XInternAtom(_display, "_NET_WM_STATE", 0);
            IntPtr atomSkip = XInternAtom(_display, "_NET_WM_STATE_SKIP_TASKBAR", 0);
            IntPtr atomSkipPager = XInternAtom(_display, "_NET_WM_STATE_SKIP_PAGER", 0);
            if (atomState != IntPtr.Zero)
            {
                var stateData = new List<ulong>();
                if (atomSkip != IntPtr.Zero) stateData.Add((ulong)atomSkip);
                if (atomSkipPager != IntPtr.Zero) stateData.Add((ulong)atomSkipPager);

                if (stateData.Count > 0)
                {
                    XChangeProperty(_display, wnd, atomState, XInternAtom(_display, "ATOM", 0), 32, PropModeReplace, stateData.ToArray(), stateData.Count);
                }
            }

            // 设置窗口不接受输入焦点
            IntPtr atomHints = XInternAtom(_display, "WM_HINTS", 0);
            if (atomHints != IntPtr.Zero)
            {
                // WM_HINTS结构：flags(32位) + input(32位) + 其他字段...
                // 我们只设置flags和input字段
                var hints = new ulong[2];
                hints[0] = 1u << 0; // InputHint flag
                hints[1] = (focusable) ? 1u : 0u;      // Input = False (不接受输入)
                XChangeProperty(_display, wnd, atomHints, XInternAtom(_display, "WM_HINTS", 0), 32, PropModeReplace, hints, 2);
            }

            XFlush(_display);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "X11 SetWindowNoFocus failed");
        }
    }

    public void SetHitTestVisible(Window window, bool visible)
    {
        try
        {
            var ph = window.TryGetPlatformHandle();
            if (ph is null) return;
            IntPtr wnd = ph.Handle;

            if (_display == IntPtr.Zero) return;
            
            XWindowAttributes attrs = new();
            attrs.override_redirect = visible? 0: 1;
            XChangeWindowAttributes(_display, wnd, (int)CW.OverrideRedirect, ref attrs);

            if (XFixesQueryExtension(_display, out _, out _) != 0)
            {
                if (visible)
                {
                    IntPtr fullRegion = XFixesCreateRegion(_display, new[]
                    {
                        new XRectangle { x = 0, y = 0, width = (ushort)window.Width, height = (ushort)window.Height }
                    }, 1);
                    XFixesSetWindowShapeRegion(_display, wnd, ShapeInput, 0, 0, fullRegion);
                    XFixesDestroyRegion(_display, fullRegion);
                }
                else
                {
                    XFixesSetWindowShapeRegion(_display, wnd, ShapeInput, 0, 0, IntPtr.Zero);
                }
            }
        }
        catch { }
    }

    public bool GetEffectiveVisible(Window window)
    {
        throw new NotImplementedException();
    }

    public void SetCloaked(Window window, bool cloaked)
    {
        var ph = window.TryGetPlatformHandle();
        if (ph is null) return;
        IntPtr wnd = ph.Handle;

        if (cloaked)
        { 
            // IntPtr atomType = XInternAtom(_display, "_NET_WM_WINDOW_TYPE", 0);
            // IntPtr atomDock = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DOCK", 0);
            // if (atomType != IntPtr.Zero && atomDock != IntPtr.Zero)
            // {
            //     ulong[] data = { (ulong)atomDock };
            //     XChangeProperty(_display, wnd, atomType, XA_ATOM, 32, PropModeReplace, data, 1);
            // }
            
            window.Hide();
        }
        else
        {
            window.Show();
            window.Activate();
        }

        XFlush(_display);
    }

    public bool AnyModelDialogOpened(Window window)
    {
        if (window.TryGetPlatformHandle() is not { } handle) return false;
        var ownerWindow = handle.Handle;
        var dialogFound = false;
        XForEachTopWindow((win) =>
        {
            if (win == ownerWindow)
            {
                dialogFound = true;
            }
        });
        return dialogFound;
    }

    public PixelPoint GetPointer()
    {
        XQueryPointer(_display, _rootWindow, out _, out _,
                out var rootX, out var rootY, out _, out _, out _);
        return new PixelPoint(rootX, rootY);
    }

    public void WindowPickerHook(Window overlay, Action<PixelPoint, EventType> hook)
    {
        var handle = overlay.TryGetPlatformHandle()?.Handle;
        _scanSkipWindowHandle = handle?? IntPtr.Zero;
        GrabMouseHook(hook);
    }


    public void RegisterFocusChanged(Action handler)
    {
        _focusChangedHook = handler;
    }

    public Bitmap Capture(IVisualElement? window, PixelRect rect)
    {
        try
        {

            IntPtr wnd = _rootWindow;
            PixelRect captureRect = rect;
            if (window != null)
            {
                wnd = window.NativeWindowHandle;
                captureRect = window.BoundingRectangle.WithX(0).WithY(0);
            }
            if (wnd == IntPtr.Zero)
            {
                throw new ArgumentException("Invalid visual element: window handle is zero", nameof(window));
            }
            if (captureRect.Width <= 0 || captureRect.Height <= 0)
            {
                throw new ArgumentException($"Invalid visual element bounding rectangle: {captureRect}", nameof(window));
            }
            return StandardXGetImageCapture(wnd, captureRect);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Capture(element) failed for element {elementType} {elementId}",
                window?.Type.ToString() ?? "null", window?.Id ?? "null");
            throw;
        }
    }

    private Bitmap StandardXGetImageCapture(IntPtr drawable, PixelRect rect)
    {
        // 验证drawable是否有效
        if (!IsValidDrawable(drawable))
        {
            _logger.LogError("Invalid drawable: {drawable}", drawable.ToString("X"));
            throw new InvalidOperationException("Invalid drawable");
        }

        _logger.LogDebug("Calling XGetImage with drawable={drawable}, x={x}, y={y}, w={w}, h={h}",
            drawable.ToString("X"), rect.X, rect.Y, rect.Width, rect.Height);

        var image = XGetImage(_display, drawable, rect.X, rect.Y,
            (uint)rect.Width, (uint)rect.Height, AllPlanes, ZPixmap);
        if (image == IntPtr.Zero)
        {
            _logger.LogError("XGetImage returned null for drawable {drawable} at ({x},{y}) size {width}x{height}",
                drawable.ToString("X"), rect.X, rect.Y, rect.Width, rect.Height);
            throw new InvalidOperationException($"XGetImage failed for drawable {drawable}");
        }

        try
        {
            var ximage = Marshal.PtrToStructure<XImage>(image);

            // 检查像素格式
            _logger.LogDebug("XImage format: depth={depth}, bits_per_pixel={bpp}, bytes_per_line={bpl}",
                ximage.depth, ximage.bits_per_pixel, ximage.bytes_per_line);

            var stride = ximage.bytes_per_line;
            int bufferSize = stride * ximage.height;
            byte[] pixelData = new byte[bufferSize];
            Marshal.Copy(ximage.data, pixelData, 0, bufferSize);

            // 根据像素格式进行转换
            ConvertPixelFormat(pixelData, ximage, rect.Width, rect.Height, stride);

            unsafe
            {
                fixed (byte* p = pixelData)
                {
                    return new Bitmap(
                        Avalonia.Platform.PixelFormat.Bgra8888,
                        Avalonia.Platform.AlphaFormat.Unpremul,
                        new nint(p),
                        new PixelSize(rect.Width, rect.Height),
                        new Vector(96, 96),
                        stride);
                }
            }
        }
        finally
        {
            XDestroyImage(image);
        }
    }

    private void ConvertPixelFormat(byte[] pixelData, XImage ximage, int width, int height, int stride)
    {
        // 检查是否需要转换像素格式
        bool needsConversion = false;

        // 常见的X11像素格式
        if (ximage.bits_per_pixel == 24 && ximage.depth == 24)
        {
            // RGB 24位 -> BGRA 32位
            ConvertRGB24ToBGRA32(pixelData, width, height, stride);
            needsConversion = true;
        }
        else if (ximage.bits_per_pixel == 32 && ximage.depth == 24)
        {
            // 32位格式，假设是RGBA或BGRA
            // 由于没有颜色掩码信息，我们暂时不进行转换
            _logger.LogDebug("Using 32-bit format as-is");
            return;
        }
        else if (ximage.bits_per_pixel == 16 && ximage.depth == 16)
        {
            // RGB16 565格式
            ConvertRGB16ToBGRA32(pixelData, width, height, stride);
            needsConversion = true;
        }

        if (!needsConversion)
        {
            _logger.LogDebug("No pixel format conversion needed for {bpp} bits per pixel", ximage.bits_per_pixel);
        }
    }

    private void ConvertRGB24ToBGRA32(byte[] rgbData, int width, int height, int stride)
    {
        // 创建新的BGRA缓冲区
        byte[] bgraData = new byte[height * stride];

        for (int y = 0; y < height; y++)
        {
            int srcRowStart = y * (width * 3); // RGB24每行3字节
            int dstRowStart = y * stride;

            for (int x = 0; x < width; x++)
            {
                int srcPixel = srcRowStart + x * 3;
                int dstPixel = dstRowStart + x * 4;

                if (srcPixel + 2 < rgbData.Length && dstPixel + 3 < bgraData.Length)
                {
                    byte r = rgbData[srcPixel];
                    byte g = rgbData[srcPixel + 1];
                    byte b = rgbData[srcPixel + 2];

                    bgraData[dstPixel] = b;     // B
                    bgraData[dstPixel + 1] = g; // G
                    bgraData[dstPixel + 2] = r; // R
                    bgraData[dstPixel + 3] = 255; // A
                }
            }
        }

        // 复制回原数组
        Array.Copy(bgraData, 0, rgbData, 0, Math.Min(bgraData.Length, rgbData.Length));
        _logger.LogDebug("Converted RGB24 to BGRA32");
    }

    private void ConvertRGB16ToBGRA32(byte[] rgb16Data, int width, int height, int stride)
    {
        // 创建新的BGRA缓冲区
        byte[] bgraData = new byte[height * stride];

        for (int y = 0; y < height; y++)
        {
            int srcRowStart = y * (width * 2); // RGB16每行2字节
            int dstRowStart = y * stride;

            for (int x = 0; x < width; x++)
            {
                int srcPixel = srcRowStart + x * 2;
                int dstPixel = dstRowStart + x * 4;

                if (srcPixel + 1 < rgb16Data.Length && dstPixel + 3 < bgraData.Length)
                {
                    // RGB16 565格式
                    ushort pixel = (ushort)(rgb16Data[srcPixel] | (rgb16Data[srcPixel + 1] << 8));

                    byte r = (byte)((pixel >> 11) & 0x1F);
                    byte g = (byte)((pixel >> 5) & 0x3F);
                    byte b = (byte)(pixel & 0x1F);

                    // 扩展到8位
                    r = (byte)(r << 3 | r >> 2);
                    g = (byte)(g << 2 | g >> 4);
                    b = (byte)(b << 3 | b >> 2);

                    bgraData[dstPixel] = b;     // B
                    bgraData[dstPixel + 1] = g; // G
                    bgraData[dstPixel + 2] = r; // R
                    bgraData[dstPixel + 3] = 255; // A
                }
            }
        }

        // 复制回原数组
        Array.Copy(bgraData, 0, rgb16Data, 0, Math.Min(bgraData.Length, rgb16Data.Length));
        _logger.LogDebug("Converted RGB16 to BGRA32");
    }

    private bool IsValidDrawable(IntPtr drawable)
    {
        try
        {
            // 尝试获取drawable的几何信息来验证其有效性
            var result = XGetGeometry(_display, drawable,
                out _, out _, out _, out _, out _, out _, out _);
            return result != 0;
        }
        catch
        {
            return false;
        }
    }

    private class X11WindowVisualElement(
        X11DisplayBackend backend,
        IntPtr windowHandle
    ) : IVisualElement
    {
        public IVisualElementContext Context => backend.Context!;
        public string Id => windowHandle.ToString("X");
        public IVisualElement? Parent
        {
            get
            {
                XQueryTree(backend._display, windowHandle, out _, out var parentHandle, out _, out _);
                // while (parentHandle != IntPtr.Zero && parentHandle != backend._rootWindow)
                // {
                //     XQueryTree(backend._display, parentHandle, out _, out var current, out _, out var count);
                //     if (count > 1) break;
                //     parentHandle = current;
                // } TODO: compress ok ? current disabled
                if (parentHandle != IntPtr.Zero
                    && parentHandle != backend._rootWindow)
                {
                    return backend.GetWindowElement(parentHandle);
                }
                return null;
            }
        }
        public IEnumerable<IVisualElement> Children
        {
            get
            {
                XQueryTree(backend._display, windowHandle, out _, out _, out var childrenPtr, out var count);
                for (var i = 0; i < count; i++)
                {
                    var child = Marshal.ReadIntPtr(childrenPtr, i * IntPtr.Size);
                    if (child != IntPtr.Zero)
                    {
                        yield return backend.GetWindowElement(child);
                    }
                }
                XFree(childrenPtr);
            }
        }
        public IVisualElement? PreviousSibling
        {
            get
            {
                XQueryTree(backend._display, windowHandle, out _,
                    out var parentHandle, out _, out var _);
                if (parentHandle != IntPtr.Zero)
                {
                    var parent = backend.GetWindowElement(parentHandle);
                    var siblings = parent.Children.ToList();
                    var index = siblings.FindIndex(c => c.NativeWindowHandle == windowHandle);
                    if (index > 0 && index < siblings.Count)
                    {
                        return siblings[index - 1];
                    }
                }
                return null;
            }
        }
        public IVisualElement? NextSibling
        {
            get
            {
                XQueryTree(backend._display, windowHandle, out _, out var parentHandle, out _, out var _);
                if (parentHandle != IntPtr.Zero)
                {
                    var parent = backend.GetWindowElement(parentHandle);
                    var siblings = parent.Children.ToList();
                    var index = siblings.FindIndex(c => c.NativeWindowHandle == windowHandle);
                    if (index >= 0 && index < siblings.Count - 1)
                    {
                        return siblings[index + 1];
                    }
                }
                return null;
            }
        }
        public VisualElementType Type => VisualElementType.TopLevel;
        public VisualElementStates States => VisualElementStates.None;
        public string? Name
        {
            get
            {
                try
                {
                    var display = backend._display;
                    if (XFetchName(display, windowHandle, out var ptr) != 0 && ptr != IntPtr.Zero)
                    {
                        var name = Marshal.PtrToStringAnsi(ptr);
                        XFree(ptr);
                        return name;
                    }
                }
                catch { }
                return null;
            }
        }
        public PixelRect BoundingRectangle
        {
            get
            {
                try
                {
                    var display = backend._display;

                    // 验证窗口句柄是否有效
                    if (windowHandle == IntPtr.Zero)
                    {
                        backend._logger.LogWarning("BoundingRectangle called with zero window handle");
                        return default(PixelRect);
                    }
                    XGetWindowAttributes(backend._display, windowHandle, out var windowAttr);
                    var x = windowAttr.x;
                    var y = windowAttr.y;
                    var w = windowAttr.width;
                    var h = windowAttr.height;
                    var result = XTranslateCoordinates(
                        display,
                        windowHandle,
                        backend._rootWindow,
                        0, 0,
                        out var absX, out var absY,
                        out _);

                    if (result == 0)
                    {
                        backend._logger.LogWarning("XTranslateCoordinates failed for window {window}", windowHandle.ToString("X"));
                        return new PixelRect(x, y, w, h);
                    }

                    return new PixelRect(absX, absY, w, h);
                }
                catch (Exception ex)
                {
                    backend._logger.LogError(ex, "BoundingRectangle failed for window {window}", windowHandle.ToString("X"));
                    return default(PixelRect);
                }
            }
        }
        public int ProcessId
        {
            get
            {
                var pid = 0;
                var win = this;
                while (win != null)
                {
                    pid = backend.XGetWindowPid(win.NativeWindowHandle);
                    if (pid != 0)
                    {
                        break;
                    }
                    win = (X11WindowVisualElement?)win.Parent;
                }
                return pid;
            }
        }
        public IntPtr NativeWindowHandle => windowHandle;
        public string? GetText(int maxLength = -1) => Name;
        public string? GetSelectionText()
        {
            throw new NotImplementedException();
        }

        public void Invoke()
        {
            throw new NotImplementedException();
        }

        public void SetText(string text)
        {
            throw new NotImplementedException();
        }

        public void SendShortcut(KeyboardShortcut shortcut)
        {
            throw new NotImplementedException();
        }

        public Task<Bitmap> CaptureAsync() => Task.FromResult(backend.Capture(this, BoundingRectangle));
    }

    private class X11ScreenVisualElement(
        X11DisplayBackend backend,
        int index
    ) : IVisualElement
    {
        private int ScreenCount => XScreenCount(backend._display);
        public IVisualElementContext Context => backend.Context!;
        public string Id => $"Screen {index}";
        public IVisualElement? Parent => null;
        public IEnumerable<IVisualElement> Children
        {
            get
            {
                var rootWindow = XRootWindow(backend._display, index);
                XQueryTree(backend._display, rootWindow, out _, out _, out var childrenPtr, out var count);
                for (var i = 0; i < count; i++)
                {
                    var child = Marshal.ReadIntPtr(childrenPtr, i * IntPtr.Size);
                    if (child != IntPtr.Zero)
                    {
                        yield return backend.GetWindowElement(child);
                    }
                }
            }
        }
        public IVisualElement? PreviousSibling => (index > 0 && index < ScreenCount) ? new X11ScreenVisualElement(backend, index - 1) : null;
        public IVisualElement? NextSibling => (index >= 0 && index < ScreenCount - 1) ? new X11ScreenVisualElement(backend, index + 1) : null;
        public VisualElementType Type => VisualElementType.Screen;
        public VisualElementStates States => VisualElementStates.None;
        public string? Name => Id;
        public PixelRect BoundingRectangle
        {
            get
            {
                var display = backend._display;
                int height = XDisplayHeight(display, index);
                int width = XDisplayWidth(display, index);
                return new PixelRect(0, 0, width, height);
            }
        }
        public int ProcessId => 0;
        public IntPtr NativeWindowHandle => XRootWindow(backend._display, index);
        public string? GetText(int maxLength = -1) => Name;
        public string? GetSelectionText() => null;

        public void Invoke() { }

        public void SetText(string text) { } // no op

        public void SendShortcut(KeyboardShortcut shortcut) { } // no op

        public Task<Bitmap> CaptureAsync() => Task.FromResult(backend.Capture(this, BoundingRectangle));
    }

    private void XThreadProcessEvent(IntPtr eventPtr)
    {
        var type = GetEventType(eventPtr);

        switch (type)
        {
            case EventType.KeyDown:
            case EventType.KeyUp:
                {
                    var evKey = Marshal.PtrToStructure<XKeyEvent>(eventPtr);
                    var state = evKey.state;
                    var norm = state & ~(LockMask | Mod2Mask);
                    var key = KeycodeToAvaloniaKey(evKey.keycode);
                    var modifiers = KeyStateToModifier(norm);
                    if (_keyboardHook != null)
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            _keyboardHook?.Invoke(new KeyboardShortcut(key, modifiers), type);
                        });
                    }
                    if (type == EventType.KeyDown)
                    {
                        var keycode = (int)evKey.keycode;
                        // iterate over a snapshot to avoid concurrent-enumeration / ToString recursion issues
                        foreach (var kv in _regs)
                        {
                            var info = kv.Value;
                            if (info.Keycode == keycode && info.Mods == norm)
                            {
                                ThreadPool.QueueUserWorkItem(_ =>
                                {
                                    info.Handler();
                                });
                            }
                        }
                    }
                }
                break;
            case EventType.MouseDown:
            case EventType.MouseUp:
            case EventType.MouseDrag:
                {
                    var buttonEvent = Marshal.PtrToStructure<XButtonEvent>(eventPtr);
                    if (_mouseHook != null)
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            _mouseHook?.Invoke(
                                new PixelPoint(buttonEvent.x_root, buttonEvent.y_root),
                                type
                            );
                        });
                    }
                }
                break;
            case EventType.FocusChange:
                {
                    if (_focusChangedHook != null)
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            _focusChangedHook?.Invoke();
                        });
                    }
                }
                break;
        }
    }

    private void XThreadMain()
    {
        // Error Callback, X11 will call this when Internal Error throwed
        XSetErrorHandler(OnXError);
        Lock evLock = new Lock();
        var evPtr = Marshal.AllocHGlobal(256);
        var buf = new byte[4];
        try
        {
            int xfd = XConnectionNumber(_display);
            var fds = new PollFd[2];
            fds[0].fd = xfd; fds[0].events = POLLIN;
            fds[1].fd = _wakePipeR; fds[1].events = POLLIN;


            while (_running || !_ops.IsCompleted)
            {
                // process pending ops first
                while (_ops.TryTake(out var op))
                {
                    try { op(); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "X op failed");
                    }
                }

                // wait for either X fd or wake pipe
                int rc = poll(fds, 2, -1);
                if (rc <= 0) continue;

                // wake pipe signaled -> drain and process ops
                if ((fds[1].revents & POLLIN) != 0)
                {
                    _logger.LogDebug("Poll: wakePipe readable (fd={fd})", _wakePipeR);
                    try
                    {
                        while (true)
                        {
                            _logger.LogDebug("About to read from wakePipe fd={fd}", _wakePipeR);
                            int r = read(_wakePipeR, buf, (IntPtr)buf.Length);
                            _logger.LogDebug("Read from wakePipe fd={fd} returned={r}", _wakePipeR, r);
                            if (r > 0) continue;
                            if (r == 0) break;
                            // r < 0 -> check errno
                            var errno = Marshal.GetLastPInvokeError();
                            // EAGAIN / EWOULDBLOCK
                            const int eagain = 11;
                            if (errno is eagain) break;
                            _logger.LogWarning("Unexpected read error from wake pipe: errno={errno}", errno);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error draining wake pipe");
                    }
                    while (_ops.TryTake(out var op))
                    {
                        try { op(); }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "X op failed");
                        }
                    }
                }

                // X fd signaled -> handle pending X events
                if ((fds[0].revents & POLLIN) != 0)
                {
                    while (XPending(_display) > 0)
                    {
                        lock (evLock)
                        {
                                XNextEvent(_display, evPtr);
                                XThreadProcessEvent(evPtr);
                        }
                    }
                }
            }
        }
        finally { Marshal.FreeHGlobal(evPtr); }
    }

    private void XThreadAction(Action action)
    {
        _ops.Add(action);
        // wake x thread
        try
        {
            if (_wakePipeW != -1)
            {
                var b = new byte[1] { 1 };
                var wr = write(_wakePipeW, b, 1);
                _logger.LogDebug("Wrote wake byte to pipe (fd={fd}) result={r}", _wakePipeW, wr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write wake pipe");
        }
    }

    private class RegInfo
    {
        public int Keycode { get; set; }
        public uint Mods { get; set; }
        public Action Handler { get; set; } = () => { };
    }
}
