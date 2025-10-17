using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Everywhere.Interop;
using Microsoft.Extensions.Logging;
namespace Everywhere.Linux.Interop;

/// <summary>
/// Linux IVisualElementContext Impl:
/// Using ILinuxDisplayBackend for keyboard/mouse event and windows info.
/// Using AtspiService to access UI elements inside window.
/// </summary>
public class LinuxVisualElementContext: IVisualElementContext
{
    private readonly INativeHelper _nativeHelper;
    private readonly ILinuxDisplayBackend _backend;
    private readonly ILogger<LinuxVisualElementContext> _logger;
    public IVisualElement? KeyboardFocusedElement {
        get
        {
            var focusedWindow = _backend.GetFocusedWindowElement();
            var atspiElem = AtspiElementFromPoint(_backend.GetPointer(),
                focusedWindow);
            if (atspiElem != IntPtr.Zero)
                return GetAtspiVisualElement(() => atspiElem, windowBarrier: true);
            return focusedWindow;
        }
    }

    public LinuxVisualElementContext Context => this;

    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;

    public LinuxVisualElementContext(INativeHelper nativeHelper, ILinuxDisplayBackend backend, ILogger<LinuxVisualElementContext> logger)
    {
        this._nativeHelper = nativeHelper;
        this._backend = backend;
        backend.Context = this;
        this._logger = logger;

        backend.RegisterFocusChanged(() =>
        {
            if (KeyboardFocusedElementChanged is not { } handler) return;
            handler(KeyboardFocusedElement);
        });
    }
    

    public IVisualElement? ElementFromPoint(PixelPoint point, PickElementMode mode = PickElementMode.Element)
    {
        var window = _backend.GetWindowElementAt(point);
        switch (mode)
        {
            case PickElementMode.Element:
                var atspiElem = AtspiElementFromPoint(point, window);
                if (atspiElem == IntPtr.Zero)
                {
                    // Fallback to window level
                    return window;
                }
                return GetAtspiVisualElement(() => atspiElem);
            case PickElementMode.Window:
                return window;
            case PickElementMode.Screen:
                return _backend.GetScreenElement();
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    public IVisualElement? ElementFromPointer(PickElementMode mode = PickElementMode.Element)
    {
        var point = _backend.GetPointer();
        return ElementFromPoint(point, mode);
    }

    public Task<IVisualElement?> PickElementAsync(PickElementMode mode)
    {
        // TODO
        return Task.FromResult<IVisualElement?>(null);
    }

    private IntPtr AtspiElementFromPoint(PixelPoint point, IVisualElement? appWindow)
    {
        var app = AtspiService.FindAccessibleApplication(appWindow?.ProcessId ?? 0);
        if (app == IntPtr.Zero)
            app = AtspiService.atspi_get_desktop(0);
        if (app == IntPtr.Zero || AtspiService.atspi_accessible_is_component(app) == 0)
            return IntPtr.Zero;
        var elem = AtspiService.atspi_component_get_accessible_at_point(app,
            point.X, point.Y, AtspiService.AtspiCoordTypeScreen, IntPtr.Zero);
        return elem;
    }
    
    private class AtspiVisualElement(
        LinuxVisualElementContext context,
        IntPtr element,
        bool windowBarrier
    ) : IVisualElement
    {
        public IVisualElementContext Context => context;

        public string Id
        {
            get
            {
                var idPtr = AtspiService.atspi_accessible_get_accessible_id(element, IntPtr.Zero);
                var idStr = Marshal.PtrToStringUTF8(idPtr) ?? "";
                AtspiService.g_free(idPtr);
                return idStr;
            }
        }

        public IVisualElement? Parent
        {
            get
            {
                if (element == IntPtr.Zero) return null;
                var parent = AtspiService.atspi_accessible_get_parent(element, IntPtr.Zero);
                if (parent == IntPtr.Zero) return null;
                // TODO: Get window element from backend instead of At-spi
                // if (windowBarrier && context.ElementType(parent) == VisualElementType.Window)
                //     return ...;
                return new AtspiVisualElement(context, parent, windowBarrier);
            }
        }
        
        private int ChildCount =>  AtspiService.atspi_accessible_get_child_count(element, IntPtr.Zero);
        private int IndexInParent => AtspiService.atspi_accessible_get_index_in_parent(element, IntPtr.Zero);

        public IEnumerable<IVisualElement> Children
        {
            get
            {
                if (element == IntPtr.Zero)
                    yield break;
                IntPtr child = IntPtr.Zero;
                var i = 0;
                while (i < ChildCount)
                {
                    child = AtspiService.atspi_accessible_get_child_at_index(element, i, IntPtr.Zero);
                    if (child != IntPtr.Zero)
                    {
                        yield return new AtspiVisualElement(context, child, windowBarrier);
                    }
                    i++;
                }
            }
        }
        public IVisualElement? PreviousSibling
        {
            get
            {
                if (element == IntPtr.Zero) return null;
                if (Parent == null) return null;
                return IndexInParent <= 0 ? 
                    null : Parent.Children.ElementAt(IndexInParent - 1);
            }
        }
        public IVisualElement? NextSibling
        {
            get
            {
                if (element == IntPtr.Zero) return null;
                if (Parent == null) return null;
                return IndexInParent + 1 >= Parent?.Children.Count() ? 
                    null : Parent?.Children.ElementAt(IndexInParent + 1);
            }
        }

        public VisualElementType Type
        {
            get
            {
                // TODO
                return VisualElementType.Unknown;
            }
        }

        public VisualElementStates States
        {
            get
            {
                // TODO
                return VisualElementStates.None;
            }
        }

        public string? Name
        {
            get
            {
                if (element == IntPtr.Zero)
                    return "";
                var namePtr = AtspiService.atspi_accessible_get_name(element, IntPtr.Zero);
                var name = Marshal.PtrToStringUTF8(namePtr);
                AtspiService.g_free(namePtr);
                return name;
            }
        }

        public int ProcessId
        {
            get
            {
                if (element == IntPtr.Zero)
                    return 0;
                var pid = AtspiService.atspi_accessible_get_process_id(element, IntPtr.Zero);
                return pid;
            }
        }

        public nint NativeWindowHandle
        {
            get
            {
                if (AtspiService.atspi_accessible_is_application(element) == 0)
                    return IntPtr.Zero;
                int xid = AtspiService.atspi_accessible_get_id(element, IntPtr.Zero);
                return new IntPtr(xid);
            }
        }
        public string? GetText(int maxLength = -1)
        {
            // TODO
            return "";
        }

        public PixelRect BoundingRectangle
        {
            get
            {
                var rectPtr = AtspiService.atspi_component_get_extents(element, AtspiService.AtspiCoordTypeScreen, IntPtr.Zero);
                var rect = Marshal.PtrToStructure<AtspiService.AtspiRect>(rectPtr);
                AtspiService.g_free(rectPtr);
                return new PixelRect(rect.x, rect.y, rect.width, rect.height);
            }
        }
        public Task<Bitmap> CaptureAsync()
        {
            return Task.FromResult(context._backend.Capture(this, BoundingRectangle));
        }
    }

    private IVisualElement? GetAtspiVisualElement(Func<IntPtr> provider, bool windowBarrier = true)
    {
        try
        {
            var element = provider();
            if (element == IntPtr.Zero) return null;
            return new AtspiVisualElement(this, element, windowBarrier);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
