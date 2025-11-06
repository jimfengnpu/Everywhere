using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Everywhere.Interop;
using Serilog;

namespace Everywhere.Linux.Interop;


public sealed partial class X11DisplayBackend : ILinuxDisplayBackend
{
    private IntPtr _display;
    private IntPtr _rootWindow;
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
                    Log.Logger.Error("XThread fcntl Error: {ex}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error("XThread pipe file Error: {ex}", ex.Message);
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
            Log.Logger.Error("X11 Backend Close Error: {ex}", ex.Message);
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
                Log.Logger.Warning(ex, "GrabKey op failed");
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
            try { XFlush(_display); } catch { }

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

    private IVisualElement GetWindowElement(IntPtr window, Func<IVisualElement> maker)
    {
        // 检查缓存
        if (_windowCache.TryGetValue(window, out var cachedElement))
        {
            Log.Logger.Debug("Using cached window element for {window}", window.ToString("X"));
            return cachedElement;
        }

        // 创建新对象并缓存
        var element = maker();
        Log.Logger.Information("Creating window element for {window}", window.ToString("X"));
        _windowCache[window] = element;
        return element;
    }

    public IVisualElement? GetFocusedWindowElement()
    {
        try
        {
            if (_display == IntPtr.Zero) return null;
            XGetInputFocus(_display, out var focusWindow, out _);
            if (focusWindow == IntPtr.Zero) return null;
            return GetWindowElement(focusWindow, () => new X11WindowVisualElement(this, focusWindow));
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "GetFocusedWindowElement failed");
            return null;
        }
    }

    public IVisualElement GetWindowElementAt(PixelPoint point)
    {
        XQueryPointer(_display, _rootWindow,
            out _, out var child, out _, out _, out _, out _, out _);

        // 如果child为0，说明指向的是root window本身
        if (child == IntPtr.Zero)
        {
            Log.Logger.Debug("XQueryPointer returned child=0, using root window");
            child = _rootWindow;
        }

        Log.Logger.Debug("GetWindowElementAt at ({x},{y}) -> window {window}",
            point.X, point.Y, child.ToString("X"));

        return GetWindowElement(child, () => new X11WindowVisualElement(this, child));
    }
    public IVisualElement GetScreenElement()
    {
        var screenIdx = XDefaultScreen(_display);
        var screenWindow = XRootWindow(_display, screenIdx);
        return GetWindowElement(screenWindow, () => new X11ScreenVisualElement(this, screenIdx));
    }

    public void SetWindowCornerRadius(Window window, CornerRadius cornerRadius)
    {
        try
        {
            var ph = window.TryGetPlatformHandle();
            if (ph is null) return;
            IntPtr wnd = ph.Handle;

            if (_display == IntPtr.Zero) return;

            XGetGeometry(_display, wnd, out _, out _, out _, out var width, out var height, out _, out _);
            if (width == 0 || height == 0) return;

            if (XShapeQueryExtension(_display, out _, out _) == 0)
            {
                Log.Logger.Warning("XShape extension not available, cannot set window corner radius");
                return;
            }

            IntPtr mask = XCreatePixmap(_display, wnd, width, height, 1);
            IntPtr gc = XCreateGC(_display, mask, 0, IntPtr.Zero);

            XSetForeground(_display, gc, 0);
            XFillRectangle(_display, mask, gc, 0, 0, width, height);

            XSetForeground(_display, gc, 1);
            const int Arc90Degrees = 90 * 64;
            int r = (int)cornerRadius.TopLeft;
            if (r <= 0) r = 8;

            // Draw Arc
            XFillArc(_display, mask, gc, 0, 0, (uint)(2 * r), (uint)(2 * r),
             2 * Arc90Degrees, Arc90Degrees); // Top-left
            XFillArc(_display, mask, gc, (int)(width - 2 * r), 0, (uint)(2 * r), (uint)(2 * r),
             3 * Arc90Degrees, Arc90Degrees);
            XFillArc(_display, mask, gc, 0, (int)(height - 2 * r), (uint)(2 * r), (uint)(2 * r),
             Arc90Degrees, Arc90Degrees);
            XFillArc(_display, mask, gc, (int)(width - 2 * r), (int)(height - 2 * r), (uint)(2 * r),
             (uint)(2 * r), 0, Arc90Degrees);

            // Fill Rects
            XFillRectangle(_display, mask, gc, r, 0, (uint)(width - 2 * r), height);
            XFillRectangle(_display, mask, gc, 0, r, width, (uint)(height - 2 * r));

            // Apply the shape mask to the window
            XShapeCombineMask(_display, wnd, ShapeBounding, 0, 0, mask, ShapeSet);
            XFreeGC(_display, gc);
            XFreePixmap(_display, mask);
            XFlush(_display);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "X11 SetWindowCornerRadius failed");
        }
    }

    public void SetWindowFocus(Window window, bool focusable)
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
            Log.Logger.Error(ex, "X11 SetWindowNoFocus failed");
        }
    }

    public void SetWindowHitTestInvisible(Window window)
    {
        try
        {
            var ph = window.TryGetPlatformHandle();
            if (ph is null) return;
            IntPtr wnd = ph.Handle;

            if (_display == IntPtr.Zero) return;

            // 使用XFixesSetWindowShapeRegion设置空输入区域
            if (XFixesQueryExtension(_display, out _, out _) != 0)
            {
                XFixesSetWindowShapeRegion(_display, wnd, ShapeInput, 0, 0, IntPtr.Zero);
            }
        }
        catch { }
    }

    public void SetWindowAsOverlay(Window window)
    {
        try
        {
            var ph = window.TryGetPlatformHandle();
            if (ph is null) return;
            IntPtr wnd = ph.Handle;

            // 1. override_redirect
            XWindowAttributes attrs = new();
            attrs.override_redirect = 1;
            XChangeWindowAttributes(_display, wnd, (int)CW.OverrideRedirect, ref attrs);

            // 2. 设置窗口类型为 DOCK（关键！）
            IntPtr atomType = XInternAtom(_display, "_NET_WM_WINDOW_TYPE", 0);
            IntPtr atomDock = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DOCK", 0);
            if (atomType != IntPtr.Zero && atomDock != IntPtr.Zero)
            {
                ulong[] data = { (ulong)atomDock };
                XChangeProperty(_display, wnd, atomType, XA_ATOM, 32, PropModeReplace, data, 1);
            }

            // 3. 确保输入区域为空
            if (XFixesQueryExtension(_display, out _, out _) != 0)
            {
                XFixesSetWindowShapeRegion(_display, wnd, ShapeInput, 0, 0, IntPtr.Zero);
            }

            XFlush(_display);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "SetWindowAsOverlay failed");
        }
    }

    public PixelPoint GetPointer()
    {
        XQueryPointer(_display, _rootWindow, out _, out _,
                out var rootX, out var rootY, out _, out _, out var mask);
        var state = 0;
        if ((mask & Button1Mask) != 0) state |= (1 << (int)MouseButton.Left);
        if ((mask & Button2Mask) != 0) state |= (1 << (int)MouseButton.Middle);
        if ((mask & Button3Mask) != 0) state |= (1 << (int)MouseButton.Right);
        return new PixelPoint(rootX, rootY);
    }

    public void WindowPickerHook(Func<PixelPoint, PixelRect> hook)
    {
        // create overlay
        var root = XDefaultRootWindow(_display);
        int hovn;
        XQueryTree(_display, root, out _, out _, out var wins, out var nwins);
        XGetWindowAttributes(_display, root, out var rootAttr);
        XMatchVisualInfo(_display, XDefaultScreen(_display), 32, TrueColor, out var vinfo);
        var cmap = XCreateColormap(_display, root, vinfo.visual, 0);
        var swa = new XSetWindowAttributes
        {
            colormap = cmap,
            background_pixel = 0,
            border_pixel = 0,
            override_redirect = 1
        };
        var overlay = XCreateWindow(
            _display, root,
            0, 0, (uint)rootAttr.width, (uint)rootAttr.height,
            0, vinfo.depth, (int)CreateWindowArgs.InputOutput,
            vinfo.visual,
            (int)(CW.Colormap | CW.BackPixel | CW.BorderPixel | CW.OverrideRedirect),
            ref swa
        );
    
        // 设置为 DOCK 类型（关键！）
        var netWmType = XInternAtom(_display, "_NET_WM_WINDOW_TYPE", 0);
        var netWmDock = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DOCK", 0);
        if (netWmType != IntPtr.Zero && netWmDock != IntPtr.Zero)
        {
            var data = new ulong[] { (ulong)netWmDock };
            XChangeProperty(_display, overlay, netWmType, (IntPtr)4, 32, 0, data, 1);
        }
        
        // 清空输入区域（穿透）
        if (XFixesQueryExtension(_display, out _, out _) != 0)
        {
            XFixesSetWindowShapeRegion(_display, overlay, ShapeInput, 0, 0, IntPtr.Zero);
        }
        // 创建用于绘制高亮边框的 GC（XOR 模式）
        var gcval = new XGCValues
        {
            foreground = XWhitePixel(_display, 0),
            function = GCFunction.GXxor,
            background = XBlackPixel(_display, 0),
            plane_mask = XWhitePixel(_display, 0) ^ XBlackPixel(_display, 0),
            subwindow_mode = IncludeInferiors
        };
        var gcvalPtr = Marshal.AllocHGlobal(Marshal.SizeOf<XGCValues>());
        Marshal.StructureToPtr(gcval, gcvalPtr, false);
    
        var gc = XCreateGC(_display, overlay,
            GCMask.GCFunction | GCMask.GCForeground | GCMask.GCBackground | GCMask.GCSubwindowMode,
            gcvalPtr);
    
        // 映射窗口
        XMapWindow(_display, overlay);
        XFlush(_display);
    
        try
        {
            IVisualElement? selected = null;
            Rect maskRect = default;
    
            bool done = false;
            bool leftPressed = false;
    
            while (!done)
            {
                // 获取鼠标位置
                XQueryPointer(_display, root, out _, out var child,
                    out var rootX, out var rootY, out _, out _, out var mask);
    
                // 关键：如果 child 是 overlay，说明穿透失败！
                // 但因为我们设置了 ShapeInput=null + override_redirect，
                // child 应该是底层窗口
                var pixelPoint = new PixelPoint(rootX, rootY);
                var rect = hook(pixelPoint);
    
                // 检测鼠标点击
                bool isLeftPressed = (mask & 0x100) != 0; // Button1Mask
                if (isLeftPressed && !leftPressed)
                {
                    leftPressed = true;
                }
                else if (!isLeftPressed && leftPressed)
                {
                    leftPressed = false;
                    done = true; // 鼠标释放时结束
                }
                if (isLeftPressed)
                {
                    XClearArea(_display, overlay, 0, 0,
                        (uint)rootAttr.width, (uint)rootAttr.height, 0);
                    XDrawRectangle(_display, overlay, gc,
                        rect.X, rect.Y, (uint)rect.Width, (uint)rect.Height);
                }
    
                Thread.Sleep(16);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(gcvalPtr);
            XUnmapWindow(_display, overlay);
            XDestroyWindow(_display, overlay);
            XFlush(_display);
        }
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
            Log.Logger.Error(ex, "Capture(element) failed for element {elementType} {elementId}",
                window?.Type.ToString() ?? "null", window?.Id ?? "null");
            throw;
        }
    }

    private Bitmap StandardXGetImageCapture(IntPtr drawable, PixelRect rect)
    {
        // 验证drawable是否有效
        if (!IsValidDrawable(drawable))
        {
            Log.Logger.Error("Invalid drawable: {drawable}", drawable.ToString("X"));
            throw new InvalidOperationException("Invalid drawable");
        }

        Log.Logger.Debug("Calling XGetImage with drawable={drawable}, x={x}, y={y}, w={w}, h={h}",
            drawable.ToString("X"), rect.X, rect.Y, rect.Width, rect.Height);

        var image = XGetImage(_display, drawable, rect.X, rect.Y,
            (uint)rect.Width, (uint)rect.Height, AllPlanes, ZPixmap);
        if (image == IntPtr.Zero)
        {
            Log.Logger.Error("XGetImage returned null for drawable {drawable} at ({x},{y}) size {width}x{height}",
                drawable.ToString("X"), rect.X, rect.Y, rect.Width, rect.Height);
            throw new InvalidOperationException($"XGetImage failed for drawable {drawable}");
        }

        try
        {
            var ximage = Marshal.PtrToStructure<XImage>(image);

            // 检查像素格式
            Log.Logger.Debug("XImage format: depth={depth}, bits_per_pixel={bpp}, bytes_per_line={bpl}",
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
            Log.Logger.Debug("Using 32-bit format as-is");
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
            Log.Logger.Debug("No pixel format conversion needed for {bpp} bits per pixel", ximage.bits_per_pixel);
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
        Log.Logger.Debug("Converted RGB24 to BGRA32");
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
        Log.Logger.Debug("Converted RGB16 to BGRA32");
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
                if (parentHandle != IntPtr.Zero
                    && parentHandle != backend._rootWindow)
                {
                    return backend.GetWindowElement(parentHandle,
                        () => new X11WindowVisualElement(backend, parentHandle));
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
                        yield return backend.GetWindowElement(child,
                            () => new X11WindowVisualElement(backend, child));
                    }
                }
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
                    var parent = backend.GetWindowElement(parentHandle,
                        () => new X11WindowVisualElement(backend, parentHandle));
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
                    var parent = backend.GetWindowElement(parentHandle, () => new X11WindowVisualElement(backend, parentHandle));
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
                        Log.Logger.Warning("BoundingRectangle called with zero window handle");
                        return default(PixelRect);
                    }

                    var result = XGetGeometry(display, windowHandle, out _, out var x, out var y, out var w, out var h, out _, out _);
                    if (result == 0)
                    {
                        Log.Logger.Warning("XGetGeometry failed for window {window}", windowHandle.ToString("X"));
                        return default(PixelRect);
                    }

                    result = XTranslateCoordinates(
                        display,
                        windowHandle,
                        backend._rootWindow,
                        0, 0,
                        out var absX, out var absY,
                        out _);

                    if (result == 0)
                    {
                        Log.Logger.Warning("XTranslateCoordinates failed for window {window}", windowHandle.ToString("X"));
                        return new PixelRect(x, y, (int)w, (int)h);
                    }

                    return new PixelRect(absX, absY, (int)w, (int)h);
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "BoundingRectangle failed for window {window}", windowHandle.ToString("X"));
                    return default(PixelRect);
                }
            }
        }
        public int ProcessId
        {
            get
            {
                IntPtr atomPid = XInternAtom(backend._display, "_NET_WM_PID", 1);
                if (atomPid == IntPtr.Zero) return 0;

                XGetWindowProperty(backend._display, windowHandle, atomPid,
                    0, 1, 0, (IntPtr)XA_CARDINAL,
                    out var actualType, out var actualFormat,
                    out var nItems, out var bytesAfter, out var prop);

                if (nItems == 0 || prop == IntPtr.Zero)
                    return 0;
                try
                {
                    return Marshal.ReadInt32(prop);
                }
                finally
                {
                    XFree(prop);
                }
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
                        yield return backend.GetWindowElement(child, () => new X11WindowVisualElement(backend, child));
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
                        Log.Logger.Warning(ex, "X op failed");
                    }
                }

                // wait for either X fd or wake pipe
                int rc = poll(fds, 2, -1);
                if (rc <= 0) continue;

                // wake pipe signaled -> drain and process ops
                if ((fds[1].revents & POLLIN) != 0)
                {
                    Log.Logger.Debug("Poll: wakePipe readable (fd={fd})", _wakePipeR);
                    try
                    {
                        while (true)
                        {
                            Log.Logger.Debug("About to read from wakePipe fd={fd}", _wakePipeR);
                            int r = read(_wakePipeR, buf, (IntPtr)buf.Length);
                            Log.Logger.Debug("Read from wakePipe fd={fd} returned={r}", _wakePipeR, r);
                            if (r > 0) continue;
                            if (r == 0) break;
                            // r < 0 -> check errno
                            var errno = Marshal.GetLastPInvokeError();
                            // EAGAIN / EWOULDBLOCK
                            const int eagain = 11;
                            if (errno is eagain) break;
                            Log.Logger.Warning("Unexpected read error from wake pipe: errno={errno}", errno);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Warning(ex, "Error draining wake pipe");
                    }
                    while (_ops.TryTake(out var op))
                    {
                        try { op(); }
                        catch (Exception ex)
                        {
                            Log.Logger.Warning(ex, "X op failed");
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
                Log.Logger.Debug("Wrote wake byte to pipe (fd={fd}) result={r}", _wakePipeW, wr);
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to write wake pipe");
        }
    }

    private class RegInfo
    {
        public int Keycode { get; set; }
        public uint Mods { get; set; }
        public Action Handler { get; set; } = () => { };
    }
}
