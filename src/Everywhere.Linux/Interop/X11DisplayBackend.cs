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
    private Action<KeyboardHotkey, bool>? _inputHook;
    private Action? _focusChangedHook;
    private int _wakePipeR = -1;
    private int _wakePipeW = -1;

    public bool IsAvailable => _display != IntPtr.Zero;
    public IVisualElementContext? Context { get; set; }

    public bool Open()
    {

        XInitThreads();
        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero) return false;
        _rootWindow = XDefaultRootWindow(_display);
        // select key events on root window so we receive KeyPress/KeyRelease
        XSelectInput(_display, _rootWindow, KeyPressMask | KeyReleaseMask | ButtonPressMask | ButtonReleaseMask | FocusChangeMask);
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

    public int GrabKey(KeyboardHotkey hotkey, Action handler)
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

    public void UngrabKey(int id)
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

    public void GrabAll(Action<KeyboardHotkey, bool> hook)
    {
        _inputHook = hook;
        XThreadAction(() =>
        {
            XGrabKeyboard(_display, _rootWindow,
                0, GrabModeAsync, GrabModeAsync, CurrentTime);
            XFlush(_display);
        });
    }

    public void UngrabAll()
    {
        _inputHook = null;
        XThreadAction(() =>
        {
            XUngrabKeyboard(_display, _rootWindow);
            // In X11 UngrabKey do not remove focus, set focus to root
            XSetInputFocus(_display, _rootWindow, RevertToParent, CurrentTime);
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

    public IVisualElement? GetFocusedWindowElement()
    {
        try
        {
            if (_display == IntPtr.Zero) return null;
            XGetInputFocus(_display, out var focusWindow, out _);
            if (focusWindow == IntPtr.Zero) return null;
            return new X11WindowVisualElement(this, focusWindow);
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
        return new X11WindowVisualElement(this, child);
    }
    public IVisualElement GetScreenElement()
    {
        var screenIdx = XDefaultScreen(_display);
        return new X11ScreenVisualElement(this, screenIdx);
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

    public void SetWindowNoFocus(Window window)
    {
        try
        {
            var ph = window.TryGetPlatformHandle();
            if (ph is null) return;
            IntPtr wnd = ph.Handle;

            if (_display == IntPtr.Zero) return;

            // 设置跳过任务栏状态
            IntPtr atomState = XInternAtom(_display, "_NET_WM_STATE", 0);
            IntPtr atomSkip = XInternAtom(_display, "_NET_WM_STATE_SKIP_TASKBAR", 0);
            if (atomState != IntPtr.Zero && atomSkip != IntPtr.Zero)
            {
                ulong[] data = { (ulong)atomSkip };
                XChangeProperty(_display, wnd, atomState, XInternAtom(_display, "ATOM", 0), 32, PropModeReplace, data, 1);
            }
        }
        catch
        {
            Log.Logger.Error("X11 SetWindowNoFocus failed");
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
    public PixelPoint GetPointer()
    {
        XQueryPointer(_display, _rootWindow, out _, out _,
                out var rootX, out var rootY, out _, out _, out _);
        return new PixelPoint(rootX, rootY);
    }

    public void RegisterFocusChanged(Action handler)
    {
        _focusChangedHook = handler;
    }

    public Bitmap Capture(IVisualElement window, PixelRect rect)
    {
        try
        {
            IntPtr wnd = window.NativeWindowHandle;
            if (wnd == IntPtr.Zero) throw new InvalidOperationException("Invalid window handle");
            var image = XGetImage(_display, wnd, rect.X, rect.Y,
                (uint)rect.Width, (uint)rect.Height, AllPlanes, ZPixmap);
            if (image == IntPtr.Zero)
                throw new InvalidOperationException("XGetImage failed");
            try
            {
                var ximage = Marshal.PtrToStructure<XImage>(image);
                var stride = ximage.bytes_per_line;
                return new Bitmap(
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Unpremul,
                    ximage.data,
                    new PixelSize(rect.Width, rect.Height),
                    new Vector(96, 96),
                    stride);
            }
            finally { XDestroyImage(image); }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Capture(window) failed");
            throw;
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
                if (parentHandle != IntPtr.Zero &&
                    parentHandle != backend._rootWindow)
                {
                    return new X11WindowVisualElement(backend, parentHandle);
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
                        yield return new X11WindowVisualElement(backend, child);
                    }
                }
            }
        }
        public IVisualElement? PreviousSibling
        {
            get
            {
                XQueryTree(backend._display, windowHandle, out _, out var parentHandle, out _, out var _);
                if (parentHandle != IntPtr.Zero)
                {
                    var parent = new X11WindowVisualElement(backend, parentHandle);
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
                    var parent = new X11WindowVisualElement(backend, parentHandle);
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
                var display = backend._display;
                XGetGeometry(display, windowHandle, out _, out var x, out var y, out var w, out var h, out _, out _);
                XTranslateCoordinates(
                    display,
                    windowHandle,
                    backend._rootWindow,
                    0, 0,
                    out var absX, out var absY,
                    out _);
                return new PixelRect(absX, absY, (int)w, (int)h);
            }
        }
        public int ProcessId
        {
            get
            {
                IntPtr atomPID = XInternAtom(backend._display, "_NET_WM_PID", 1);
                if (atomPID == IntPtr.Zero) return 0;

                XGetWindowProperty(backend._display, windowHandle, atomPID,
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
                        yield return new X11WindowVisualElement(backend, child);
                    }
                }
            }
        }
        public IVisualElement? PreviousSibling => (index > 0 && index < ScreenCount) ? new X11ScreenVisualElement(backend, index - 1) : null;
        public IVisualElement? NextSibling => (index >= 0 && index < ScreenCount - 1) ? new X11ScreenVisualElement(backend, index + 1) : null;
        public VisualElementType Type => VisualElementType.Screen;
        public VisualElementStates States => VisualElementStates.None;
        public string? Name => null;
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
        public IntPtr NativeWindowHandle => IntPtr.Zero;
        public string? GetText(int maxLength = -1) => Name;
        public Task<Bitmap> CaptureAsync() => Task.FromResult(backend.Capture(this, BoundingRectangle));
    }

    private void XThreadMain()
    {
        // Error Callback, X11 will call this when Internal Error throwed
        XSetErrorHandler(OnXError);
        Lock evLock = new Lock();
        var evPtr = Marshal.AllocHGlobal(256);
        // var evCopyPtr = Marshal.AllocHGlobal(256);
        var buf = new byte[4];
        try
        {
            int xfd = XConnectionNumber(_display);
            var fds = new pollfd[2];
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
                    try
                    {
                        while (XPending(_display) > 0)
                        {
                            XAnyEvent e;
                            XKeyEvent evKey;
                            lock (evLock)
                            {
                                XNextEvent(_display, evPtr);
                                e = Marshal.PtrToStructure<XAnyEvent>(evPtr);
                                evKey = Marshal.PtrToStructure<XKeyEvent>(evPtr);
                                // Marshal.StructureToPtr(e, evCopyPtr, false);
                                // Marshal.Copy(evPtr, evBuffer, 0, 256);
                            }
                            if (e.type is KeyPress or KeyRelease)
                            {
                                // evKey = Marshal.PtrToStructure<XKeyEvent>(Marshal. evBuffer);
                                Log.Logger.Information("X recv key={key},mod={mod},press={press}",
                                    evKey.keycode, evKey.state, evKey.type == KeyPress);
                                var state = evKey.state;
                                var norm = state & ~(LockMask | Mod2Mask);
                                var key = KeycodeToAvaloniaKey(evKey.keycode);
                                var modifiers = KeyStateToModifier(norm);
                                if (_inputHook != null)
                                {
                                    ThreadPool.QueueUserWorkItem(_ =>
                                    {
                                        _inputHook?.Invoke(new KeyboardHotkey(key, modifiers), evKey.type == KeyPress);
                                    });
                                }
                                if (evKey.type == KeyPress)
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
                            else if (e.type is FocusIn or FocusOut)
                            {
                                if (_focusChangedHook != null)
                                {
                                    ThreadPool.QueueUserWorkItem(_ =>
                                    {
                                        _focusChangedHook?.Invoke();
                                    });
                                }
                            }
                        }
                    }
                    catch { }
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
                var wr = write(_wakePipeW, b, (IntPtr)1);
                Log.Logger.Debug("Wrote wake byte to pipe (fd={fd}) result={r}", _wakePipeW, wr);
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to write wake pipe");
        }
    }

    private int OnXError(IntPtr d, IntPtr errorEventPtr)
    {
        try
        {
            var ev = Marshal.PtrToStructure<XErrorEvent>(errorEventPtr);
            string text = string.Empty;
            try
            {
                // XGetErrorText returns a pointer to a static buffer; call and marshal
                var bufPtr = XGetErrorText(d, ev.errorCode);
                if (bufPtr != IntPtr.Zero) text = Marshal.PtrToStringAnsi(bufPtr) ?? string.Empty;
            }
            catch { }

            Log.Logger.Error(
                "X Error: code={code} request={req} minor={minor} resource={res} text={text}",
                ev.errorCode,
                ev.request_code,
                ev.minor_code,
                ev.resourceid,
                text);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to marshal XErrorEvent");
        }
        return 0;
    }


    private static string KeysymToString(UIntPtr ks)
    {
        try
        {
            var p = XKeysymToString(ks);
            if (p == IntPtr.Zero) return string.Empty;
            return Marshal.PtrToStringAnsi(p) ?? string.Empty;
        }
        catch { return string.Empty; }
    }
    private static KeyModifiers KeyStateToModifier(uint state)
    {
        KeyModifiers mod = KeyModifiers.None;
        if ((state & ShiftMask) != 0) mod |= KeyModifiers.Shift;
        if ((state & ControlMask) != 0) mod |= KeyModifiers.Control;
        if ((state & Mod1Mask) != 0) mod |= KeyModifiers.Alt;
        if ((state & Mod4Mask) != 0) mod |= KeyModifiers.Meta;
        return mod;
    }

    private class RegInfo
    {
        public int Keycode { get; set; }
        public uint Mods { get; set; }
        public Action Handler { get; set; } = () => { };
    }

    #region x11 p/invoke + helpers
    // Macros from X11 headers
    // Key Mask
    private const int AnyKey = 0;
    // Modifiers Mask
    private const uint ShiftMask = 1u << 0;
    private const uint LockMask = 1u << 1;
    private const uint ControlMask = 1u << 2;
    private const uint Mod1Mask = 1u << 3;
    private const uint Mod2Mask = 1u << 4;
    private const uint Mod4Mask = 1u << 6;
    private const uint AnyModifiers = 1u << 15;
    // Event Mask
    private const uint KeyPressMask = 1u << 0;
    private const uint KeyReleaseMask = 1u << 1;
    private const uint ButtonPressMask = 1u << 2;
    private const uint ButtonReleaseMask = 1u << 3;
    private const uint FocusChangeMask = 1u << 21;
    // Event Type
    private const int KeyPress = 2;
    private const int KeyRelease = 3;
    private const int ButtonPress = 4;
    private const int ButtonRelease = 5;
    private const int FocusIn = 9;
    private const int FocusOut = 10;
    // Others
    private const int GrabModeAsync = 1;
    private const uint CurrentTime = 0;
    private const string LibX11 = "libX11.so.6";

    [StructLayout(LayoutKind.Sequential)]
    private struct XAnyEvent
    {
        public int type;
        public ulong serial;
        public int sendEvent;
        public IntPtr display;
        public IntPtr window;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct XKeyEvent
    {
        public int type;
        public ulong serial;
        public int sendEvent;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public ulong time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public uint keycode;
        public bool sameScreen;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct XErrorEvent
    {
        public int type;
        public IntPtr display;
        public ulong resourceid;
        public ulong serial;
        public byte errorCode;
        public byte request_code;
        public byte minor_code;
        public byte pad;
    }
    private delegate int XErrorHandlerFunc(IntPtr display, IntPtr errorEventPtr);
    [StructLayout(LayoutKind.Sequential)]
    private struct XImage
    {
        public int width;
        public int height;
        public int xoffset;
        public int format;
        public IntPtr data;
        public int byte_order;
        public int bitmap_unit;
        public int bitmap_bit_order;
        public int bitmap_pad;
        public int depth;
        public int bytes_per_line;
        public int bits_per_pixel;
    }

    [LibraryImport(LibX11)] private static partial IntPtr XOpenDisplay(IntPtr displayName);
    [LibraryImport(LibX11)] private static partial int XInitThreads();
    [LibraryImport(LibX11)] private static partial int XCloseDisplay(IntPtr display);
    [LibraryImport(LibX11)] private static partial int XDefaultScreen(IntPtr display);
    [LibraryImport(LibX11)] private static partial IntPtr XDefaultRootWindow(IntPtr display);
    [LibraryImport(LibX11)] private static partial int XRootWindow(IntPtr display, int screenNumber);
    [LibraryImport(LibX11)] private static partial int XScreenCount(IntPtr display);
    [LibraryImport(LibX11)] private static partial int XDisplayWidth(IntPtr display, int screenNumber);
    [LibraryImport(LibX11)] private static partial int XDisplayHeight(IntPtr display, int screenNumber);
    [LibraryImport(LibX11)] private static partial UIntPtr XStringToKeysym([MarshalAs(UnmanagedType.LPStr)] string s);
    [LibraryImport(LibX11)] private static partial int XKeysymToKeycode(IntPtr display, UIntPtr keysym);
    [LibraryImport(LibX11)] private static partial int XSelectInput(IntPtr display, IntPtr window, uint eventMask);
    [LibraryImport(LibX11)] private static partial int XFlush(IntPtr display);
    [LibraryImport(LibX11)]
    private static partial int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow,
        int ownerEvents, int pointerMode, int keyboardMode);
    [LibraryImport(LibX11)] private static partial int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow);
    [LibraryImport(LibX11)]
    private static partial int XGrabKeyboard(IntPtr display, IntPtr grabWindow, int ownerEvents,
        int pointerMode, int keyboardMode, uint time);
    [LibraryImport(LibX11)] private static partial int XUngrabKeyboard(IntPtr display, IntPtr grabWindow);
    [LibraryImport(LibX11)] private static partial int XNextEvent(IntPtr display, IntPtr ev);
    [LibraryImport(LibX11)] private static partial IntPtr XSetErrorHandler(XErrorHandlerFunc handler);
    [LibraryImport(LibX11)] private static partial IntPtr XGetErrorText(IntPtr display, int code);
    [LibraryImport(LibX11)] private static partial int XPending(IntPtr display);
    [LibraryImport(LibX11)] private static partial int XConnectionNumber(IntPtr display);
    [LibraryImport(LibX11)] private static partial UIntPtr XKeycodeToKeysym(IntPtr display, int keycode, int index);
    [LibraryImport(LibX11)] private static partial IntPtr XKeysymToString(UIntPtr keysym);

    [LibraryImport(LibX11)]
    private static partial int XTranslateCoordinates(IntPtr display,
        IntPtr srcWindow, IntPtr destWindow,
        int srcX, int srcY,
        out int destXReturn, out int destYReturn,
        out IntPtr childReturn);
    // 截图相关
    private const int ZPixmap = 2;
    private const ulong AllPlanes = ~0UL;
    [LibraryImport(LibX11)]
    private static partial IntPtr XGetImage(IntPtr display, IntPtr drawable,
        int x, int y, uint width, uint height, ulong planeMask, int format);
    [LibraryImport(LibX11)] private static partial void XDestroyImage(IntPtr ximage);
    // Xlib函数声明
    [LibraryImport(LibX11)]
    private static partial void XGetInputFocus(IntPtr display, out IntPtr focusReturn, out int revertToReturn);

    [LibraryImport(LibX11)]
    private static partial int XQueryPointer(IntPtr display, IntPtr window,
        out IntPtr rootReturn, out IntPtr childReturn,
        out int rootXReturn, out int rootYReturn,
        out int winXReturn, out int winYReturn,
        out uint maskReturn);
    [LibraryImport(LibX11)]
    private static partial int XQueryTree(IntPtr display, IntPtr window,
        out IntPtr rootReturn, out IntPtr parentReturn,
        out IntPtr childrenReturn, out int nchildrenReturn);
    // 其他
    [LibraryImport(LibX11)]
    private static partial int XGetGeometry(IntPtr display, IntPtr drawable,
        out IntPtr rootReturn, out int x, out int y, out uint width, out uint height,
        out uint borderWidth, out uint depth);
    [LibraryImport(LibX11)] private static partial int XFetchName(IntPtr display, IntPtr window, out IntPtr windowName);
    [LibraryImport(LibX11)] private static partial void XFree(IntPtr data);
    [LibraryImport(LibX11)] private static partial IntPtr XInternAtom(IntPtr display, [MarshalAs(UnmanagedType.LPStr)] string atomName, int onlyIfExists);
    private const int XA_CARDINAL = 6;
    [LibraryImport(LibX11)]
    private static partial int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
        long offset, long length, int delete, IntPtr reqType,
        out IntPtr actualTypeReturn, out int actualFormatReturn, out ulong nitemsReturn, out ulong bytesAfterReturn, out IntPtr propReturn);
    private const int RevertToPointerRoot = 1;
    private const int RevertToParent = 2;
    [LibraryImport("libX11.so.6")]
    private static partial void XSetInputFocus(IntPtr display, IntPtr window, int revert_to, uint time);

    // XChangeProperty 
    private const int PropModeReplace = 0;

    [LibraryImport(LibX11)]
    private static partial void XChangeProperty(IntPtr display, IntPtr window, IntPtr property,
        IntPtr type, int format, int mode, ulong[] data, int nelements);
    // XFixes/XShape
    // XShape Extension
    private const int ShapeBounding = 0;
    private const int ShapeClip = 1;
    private const int ShapeInput = 2;
    private const int ShapeSet = 0;
    private const int ShapeUnion = 1;

    [LibraryImport("libXfixes.so.3")]
    private static partial void XFixesSetWindowShapeRegion(IntPtr display, IntPtr window, int shapeKind, int xOffset, int yOffset, IntPtr region);

    [LibraryImport("libXfixes.so.3")]
    private static partial int XFixesQueryExtension(IntPtr display, out int eventBase, out int errorBase);


    [LibraryImport("libXext.so.6")] private static partial int XShapeQueryExtension(IntPtr display, out int eventBase, out int errorBase);
    [LibraryImport("libXext.so.6")] private static partial void XShapeCombineMask(IntPtr display, IntPtr window, int shapeKind, int xOff, int yOff, IntPtr mask, int operation);

    [LibraryImport(LibX11)] private static partial IntPtr XCreatePixmap(IntPtr display, IntPtr drawable, uint width, uint height, uint depth);
    [LibraryImport(LibX11)] private static partial IntPtr XCreateGC(IntPtr display, IntPtr drawable, ulong valuemask, IntPtr values);
    [LibraryImport(LibX11)] private static partial void XFreeGC(IntPtr display, IntPtr gc);
    [LibraryImport(LibX11)] private static partial void XSetForeground(IntPtr display, IntPtr gc, ulong color);
    [LibraryImport(LibX11)] private static partial void XFillRectangle(IntPtr display, IntPtr drawable, IntPtr gc, int x, int y, uint width, uint height);
    [LibraryImport(LibX11)] private static partial void XFillArc(IntPtr display, IntPtr drawable, IntPtr gc, int x, int y, uint width, uint height, int angle1, int angle2);
    [LibraryImport(LibX11)] private static partial void XFreePixmap(IntPtr display, IntPtr pixmap);
    // libc / polling / pipe
    [LibraryImport("libc", SetLastError = true)] private static partial int pipe([MarshalAs(UnmanagedType.LPArray, SizeConst = 2)] int[] fds);
    [LibraryImport("libc", SetLastError = true)] private static partial int write(int fd, byte[] buf, IntPtr count);
    [LibraryImport("libc", SetLastError = true)] private static partial int read(int fd, byte[] buf, IntPtr count);
    [LibraryImport("libc", SetLastError = true)] private static partial int close(int fd);

    [LibraryImport("libc", SetLastError = true)] private static partial int fcntl(int fd, int cmd, int arg);
    // fcntl and flags (used to make pipe non-blocking / close-on-exec)
    private const int F_GETFL = 3;
    private const int F_SETFL = 4;
    private const int F_SETFD = 2;
    private const int FD_CLOEXEC = 1;
    private const int O_NONBLOCK = 0x800;
    [StructLayout(LayoutKind.Sequential)]
    private struct pollfd { public int fd; public short events; public short revents; }
    private const short POLLIN = 0x0001;
    [LibraryImport("libc", SetLastError = true)]
    private static partial int poll([In, Out] pollfd[] fds, uint nfds, int timeout);

    #endregion
}
