using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Everywhere.Common;
using Everywhere.Interop;
using Serilog;

namespace Everywhere.Linux.Interop;

/// <summary>
/// AT-SPI（Assistive Technology Service Provider Interface）
/// </summary>
public partial class AtspiService
{
    private readonly bool _initialized;
    private readonly LinuxVisualElementContext _context;
    private readonly ConcurrentDictionary<IntPtr, AtspiVisualElement> _cachedElement = new();
    private IntPtr _focusedElement;
    
    public AtspiService(LinuxVisualElementContext context)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AT_SPI_BUS")))
            Environment.SetEnvironmentVariable("AT_SPI_BUS", "session");
        
        if (atspi_init() < 0)
            throw new InvalidOperationException("Failed to initialize AT-SPI");
        _context = context;
        _initialized = true;
    }

    private void CheckInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("At-SPI not initialized");
        }
    }
    
    ~AtspiService()
    {
        atspi_exit();
    }
    
    // parent must be AtspiElement
    private AtspiVisualElement? AtspiElementFromPoint(PixelPoint point, IVisualElement? parent)
    {
        
        parent ??= GetAtspiVisualElement(() => atspi_get_desktop(0));
        if (parent is null)
        {
            return null;
        }
        var elem = atspi_component_get_accessible_at_point(
            parent.NativeWindowHandle, // note: raw intptr used here
            point.X, point.Y, AtspiCoordTypeScreen, IntPtr.Zero);
        if (elem != IntPtr.Zero)
        {
            return GetAtspiVisualElement(() => elem);
        }
        foreach (var child in parent.Children)
        {
            var rect = child.BoundingRectangle;
            if ((rect.Width != 0 && rect.Height != 0) && (!rect.Contains(point)))
            {
                continue;
            }
            var result = AtspiElementFromPoint(point, child);
            if (result != null) return result;
        }
        return null;
    }

    public IVisualElement? ElementFromPoint(PixelPoint point)
    {
        CheckInitialized();
        return AtspiElementFromPoint(point, null);
    }

    public IVisualElement? ElementFocused()
    {
        return GetAtspiVisualElement(() => _focusedElement);
    }
    
    private class AtspiVisualElement(
        AtspiService atspi,
        IntPtr element
    ) : IVisualElement
    {
        public IVisualElementContext Context => atspi._context;

        public string Id
        {
            get
            {
                var idPtr = atspi_accessible_get_accessible_id(element, IntPtr.Zero);
                var idStr = Marshal.PtrToStringUTF8(idPtr) ?? "";
                g_free(idPtr);
                return idStr;
            }
        }

        ~AtspiVisualElement()
        {
            g_object_unref(element);
        }
        

        public IVisualElement? Parent
        {
            get
            {
                if (element == IntPtr.Zero) return null;
                var parent = atspi_accessible_get_parent(element, IntPtr.Zero);
                if (parent == IntPtr.Zero) return null;
                return atspi.GetAtspiVisualElement(() => parent);
            }
        }
        
        private int ChildCount =>  atspi_accessible_get_child_count(element, IntPtr.Zero);
        private int IndexInParent => element == IntPtr.Zero ? 0 
                            : atspi_accessible_get_index_in_parent(element, IntPtr.Zero);

        public IEnumerable<IVisualElement> Children
        {
            get
            {
                if (element == IntPtr.Zero)
                    yield break;
                var i = 0;
                while (i < ChildCount)
                {
                    var child = atspi_accessible_get_child_at_index(element, i, IntPtr.Zero);
                    if (child != IntPtr.Zero)
                    {
                        var childElem = atspi.GetAtspiVisualElement(() => child);
                        if (childElem is not null)
                        {
                            yield return childElem;
                        }
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
                var namePtr = atspi_accessible_get_name(element, IntPtr.Zero);
                var name = Marshal.PtrToStringUTF8(namePtr);
                g_free(namePtr);
                return name;
            }
        }

        public int ProcessId
        {
            get
            {
                if (element == IntPtr.Zero)
                    return 0;
                var pid = atspi_accessible_get_process_id(element, IntPtr.Zero);
                return pid;
            }
        }

        public nint NativeWindowHandle => element;

        public string? GetText(int maxLength = -1)
        {
            // TODO
            return "";
        }

        public PixelRect BoundingRectangle
        {
            get
            {
                var rectPtr = atspi_component_get_extents(element, AtspiCoordTypeScreen, IntPtr.Zero);
                var rect = Marshal.PtrToStructure<AtspiRect>(rectPtr);
                g_free(rectPtr);
                Log.Logger.Debug("Element {Name} BoundingRectangle: {X},{Y} - {W}x{H}", Name, rect.x, rect.y, rect.width, rect.height);
                return new PixelRect(rect.x, rect.y, rect.width, rect.height);
            }
        }
        public Task<Bitmap> CaptureAsync()
        {
            return Task.FromResult(atspi._context._backend.Capture(this, BoundingRectangle));
        }
    }

    private AtspiVisualElement? GetAtspiVisualElement(Func<IntPtr> provider)
    {
        try
        {
            var element = provider();
            if (element == IntPtr.Zero) return null;
            if (_cachedElement.TryGetValue(element, out var visualElement)) return visualElement;
            var elem = new AtspiVisualElement(this, element);
            _cachedElement[element] = elem;
            return elem;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private const string LibAtspi = "libatspi.so.0";
    public const int AtspiCoordTypeScreen = 0;
    public const int AtspiCoordTypeWindow = 1;
    public const int AtspiCoordTypeParent = 2;
    [StructLayout(LayoutKind.Sequential)]
    public struct AtspiRect
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [LibraryImport(LibAtspi)]
    public static partial int atspi_init();

    [LibraryImport(LibAtspi)]
    public static partial void atspi_exit();

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_get_desktop(int i);


    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_name(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_role_name(IntPtr accessible, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_parent(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_get_child_count(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_child_at_index(IntPtr accessible, int index, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_get_index_in_parent(IntPtr accessible, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_get_process_id(IntPtr accessible, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_get_id(IntPtr accessible, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_accessible_id(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_application(IntPtr accessible);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_component(IntPtr accessible);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_component_get_accessible_at_point(IntPtr component, int x, int y, int coordType, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_component_get_extents(IntPtr component, int coordType, IntPtr error);

    [LibraryImport("libgobject-2.0.so.0")]
    public static partial void g_object_unref(IntPtr obj);

    [LibraryImport("libglib-2.0.so.0")]
    public static partial void g_free(IntPtr mem);
}