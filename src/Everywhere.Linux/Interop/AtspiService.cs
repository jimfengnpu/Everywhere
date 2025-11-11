using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Everywhere.Common;
using Everywhere.Interop;
using Microsoft.Extensions.Logging;

namespace Everywhere.Linux.Interop;

/// <summary>
/// AT-SPI（Assistive Technology Service Provider Interface）
/// </summary>
public partial class AtspiService
{
    private readonly bool _initialized;
    private readonly LinuxVisualElementContext _context;
    private readonly ConcurrentDictionary<IntPtr, AtspiVisualElement> _cachedElement = new();
    private readonly ILogger<AtspiService> _logger = ServiceLocator.Resolve<ILogger<AtspiService>>();
    private IntPtr _focusedElement;
    
    public AtspiService(LinuxVisualElementContext context)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AT_SPI_BUS")))
            Environment.SetEnvironmentVariable("AT_SPI_BUS", "session");
        
        if (atspi_init() < 0)
            throw new InvalidOperationException("Failed to initialize AT-SPI");
        var listener = atspi_event_listener_new(OnEvent, IntPtr.Zero, IntPtr.Zero);
        atspi_event_listener_register(listener, 
            Marshal.StringToCoTaskMemUTF8("object:state-changed:focused"), IntPtr.Zero);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            atspi_event_main();
        });
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

    private static void OnEvent(IntPtr atspiEvent, IntPtr userData)
    {
        
    }
    
    
    private AtspiRect ElementBounds(IntPtr elem)
    {
        var rectPtr = atspi_component_get_extents(elem, AtspiCoordTypeScreen, IntPtr.Zero);
        var rect = Marshal.PtrToStructure<AtspiRect>(rectPtr);
        g_free(rectPtr);
        return rect;
    }

    private VisualElementStates ElementState(IntPtr elem)
    {
        try
        {
            var states = VisualElementStates.None;
            var elemStateset = atspi_accessible_get_state_set(elem);
            if (atspi_state_set_contains(elemStateset, STATE_VISIBLE) == 0) states |= VisualElementStates.Offscreen;
            if (atspi_state_set_contains(elemStateset, STATE_ENABLE) == 0) states |= VisualElementStates.Disabled;
            if (atspi_state_set_contains(elemStateset, STATE_FOCUSED) == 1) states |= VisualElementStates.Focused;
            if (atspi_state_set_contains(elemStateset, STATE_SELECTED) == 1) states |= VisualElementStates.Selected;
            if (atspi_state_set_contains(elemStateset, STATE_EDITABLE) == 0) states |= VisualElementStates.ReadOnly;
            g_free(elemStateset);
            if (atspi_accessible_get_role(elem, IntPtr.Zero) == ROLE_PASSWORD_TEXT) 
                states |= VisualElementStates.Password;
            return states;
        }
        catch (COMException)
        {
            return VisualElementStates.None;
        }
    }
    private bool ElementVisible(IntPtr elem, bool includeApp = false)
    {
        if (includeApp && atspi_accessible_is_application(elem) != 0)
        {
            return true;
        }
        if (atspi_accessible_is_component(elem) == 0) return false;
        if (ElementState(elem).HasFlag(VisualElementStates.Offscreen)) return false;
        var rect = ElementBounds(elem);
        return rect is { height: > 0, width: > 0 };
    }

    private IEnumerable<AtspiVisualElement> ElementChildren(IntPtr elem)
    {
        var count = atspi_accessible_get_child_count(elem, IntPtr.Zero);
        var i = 0;
        while (i < count)
        {
            var child = atspi_accessible_get_child_at_index(elem, i, IntPtr.Zero);
            if (child != IntPtr.Zero && ElementVisible(child, true))
            {
                var childElem = GetAtspiVisualElement(() => child);
                if (childElem != null)
                {
                    yield return childElem;
                }
            }
            i++;
        }
    }

    
    // parent must be AtspiElement
    private AtspiVisualElement? AtspiElementFromPoint(PixelPoint point, AtspiVisualElement? parent)
    {
        
        parent ??= GetAtspiVisualElement(() => atspi_get_desktop(0));
        if (parent is null)
        {
            return null;
        }
        var elem = atspi_component_get_accessible_at_point(
            parent.element, // note: raw intptr used here
            point.X, point.Y, AtspiCoordTypeScreen, IntPtr.Zero);
        if (elem != IntPtr.Zero)
        {
            return GetAtspiVisualElement(() => elem);
        }
        foreach (var child in ElementChildren(parent.element))
        {
            if (child.States.HasFlag(VisualElementStates.Offscreen))
            {
                continue;
            }
            var elemChild = atspi_component_get_accessible_at_point(
                child.element, // note: raw intptr used here
                point.X, point.Y, AtspiCoordTypeScreen, IntPtr.Zero);
            if (elemChild != IntPtr.Zero && ElementVisible(elemChild))
            {
                _logger.LogInformation("Found element");
                return GetAtspiVisualElement(() => elemChild);
            }
        }
        _logger.LogWarning("AtspiElementFromPoint not found");
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
        IntPtr elementPtr
    ) : IVisualElement, IDisposable
    {
        public IVisualElementContext Context => atspi._context;
        public readonly IntPtr element = g_object_ref(elementPtr);
        private readonly List<IntPtr> _cachedAccessibleChildren = [];
        private bool _childrenCached;
        private AtspiVisualElement? _parent => (AtspiVisualElement?)Parent;
        private bool _disposed;
        private readonly Lock _childrenLoading = new ();

        public void Dispose()
        {
            if (!_disposed && element != IntPtr.Zero)
            {
                atspi._cachedElement.TryRemove(element, out _);
                _parent?._cachedAccessibleChildren.Remove(element);
                g_object_unref(element);
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~AtspiVisualElement()
        {
            Dispose();
        }
        

        public IVisualElement? Parent
        {
            get
            {
                if (element == IntPtr.Zero) return null;
                var parent = atspi_accessible_get_parent(element, IntPtr.Zero);
                if (parent == IntPtr.Zero) return null;
                if (atspi_accessible_is_application(parent) != 0)
                {
                    return null;
                }
                return atspi.GetAtspiVisualElement(() => parent);
            }
        }

        private int IndexInParent => element == IntPtr.Zero ? 0 
                            : atspi_accessible_get_index_in_parent(element, IntPtr.Zero);

        public IEnumerable<IVisualElement> Children
        {
            get
            {
                if (element == IntPtr.Zero)
                    yield break;
                lock (_childrenLoading)
                {
                    if (!_childrenCached)
                    {
                        atspi._logger.LogInformation("Element {Name}: Get Children", Name);
                        foreach (var elem in atspi.ElementChildren(element))
                        {
                            if (!_cachedAccessibleChildren.Contains(elem.element))
                            {
                                _cachedAccessibleChildren.Add(elem.element);
                            }
                            // yield return elem;
                        }
                        _childrenCached = true;
                    }
                }
                foreach(var child in _cachedAccessibleChildren)
                {
                    yield return atspi._cachedElement[child];
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
                try
                {
                    var role = atspi_accessible_get_role(element, IntPtr.Zero);
                    return role switch
                    {
                        ROLE_APPLICATION => VisualElementType.TopLevel,
                        ROLE_BUTTON => VisualElementType.Button,
                        ROLE_CHECK_BOX => VisualElementType.CheckBox,
                        ROLE_COMBO_BOX => VisualElementType.ComboBox,
                        ROLE_DOCUMENT_EMAIL or
                            ROLE_DOCUMENT_FRAME or
                            ROLE_DOCUMENT_PRESENTATION or
                            ROLE_DOCUMENT_SPREADSHEET or
                            ROLE_DOCUMENT_TEXT or
                            ROLE_DOCUMENT_WEB => VisualElementType.Document,
                        ROLE_ENTRY or
                            ROLE_PASSWORD_TEXT => VisualElementType.TextEdit,
                        ROLE_IMAGE => VisualElementType.Image,
                        ROLE_LABEL => VisualElementType.Label,
                        ROLE_LINK  => VisualElementType.Hyperlink,
                        ROLE_LIST  => VisualElementType.ListView,
                        ROLE_LIST_ITEM => VisualElementType.ListViewItem,
                        ROLE_MENU      => VisualElementType.Menu,
                        ROLE_MENU_ITEM => VisualElementType.MenuItem,
                        ROLE_PAGE_TAB  => VisualElementType.TabItem,
                        ROLE_PANEL     => VisualElementType.Panel,
                        ROLE_PROGRESS_BAR => VisualElementType.ProgressBar,
                        ROLE_RADIO_BUTTON => VisualElementType.RadioButton,
                        ROLE_SCROLL_BAR => VisualElementType.ScrollBar,
                        ROLE_SLIDER     => VisualElementType.Slider,
                        ROLE_TABLE      => VisualElementType.Table,
                        ROLE_TABLE_ROW  => VisualElementType.TableRow,
                        ROLE_TREE       => VisualElementType.TreeViewItem,
                        ROLE_TREE_TABLE => VisualElementType.TreeView,
                        _ => VisualElementType.Unknown
                    };
                }
                catch (COMException)
                {
                    return VisualElementType.Unknown;
                }
            }
        }

        public VisualElementStates States => atspi.ElementState(element);

        public string? Name
        {
            get
            {
                try
                {
                    // return "";
                    var namePtr = atspi_accessible_get_name(element, IntPtr.Zero);
                    if (namePtr == IntPtr.Zero)
                    {
                        return "";
                    }
                    var name = Marshal.PtrToStringUTF8(namePtr);
                    g_free(namePtr);
                    return name;
                }
                catch (COMException)
                {
                    return "";
                }
            }
        }
        
        public string Id
        {
            get
            {
                var idPtr = atspi_accessible_get_accessible_id(element, IntPtr.Zero);
                if (idPtr == IntPtr.Zero)
                {
                    return "";
                }
                var idStr = Marshal.PtrToStringUTF8(idPtr) ?? "";
                g_free(idPtr);
                return idStr;
            }
        }

        public int ProcessId
        {
            get
            {
                var pid = atspi_accessible_get_process_id(element, IntPtr.Zero);
                return pid;
            }
        }

        public nint NativeWindowHandle
        {
            get
            {
                var rect = BoundingRectangle;
                return atspi._context._backend.GetWindowElementAt(rect.Center).NativeWindowHandle;
            }
        }

        public string? GetText(int maxLength = -1)
        {
            if (atspi_accessible_is_text(element) == 1)
            {
                var count = atspi_text_get_character_count(element, IntPtr.Zero);
                var rawText = atspi_text_get_text(element, 0, count, IntPtr.Zero);
                if (rawText == IntPtr.Zero)
                {
                    return "";
                }
                var text = Marshal.PtrToStringUTF8(rawText);
                g_free(rawText);
                return text;
            }
            return "";
        }

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

        public PixelRect BoundingRectangle
        {
            get
            {
                if (!atspi.ElementVisible(element))
                {
                    return new PixelRect(0, 0, 0, 0);
                }
                var rect = atspi.ElementBounds(element);
                atspi._logger.LogInformation("Element {Name} BoundingRectangle: {X},{Y} - {W}x{H}", Name, rect.x, rect.y, rect.width, rect.height);
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
            _logger.LogInformation("Element add {Name}({Type})", elem.Name, elem.Type);
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
    // Accessible Impl Type
    
    // Role
    private const int ROLE_INVALID = 0;
    private const int ROLE_LABEL = 29;
    private const int ROLE_BUTTON = 43;
    // TextEdit
    private const int ROLE_ENTRY = 79; // ATSPI_STATE_EDITABLE else Text Role
    private const int ROLE_PASSWORD_TEXT = 40;
    // Document
    private const int ROLE_DOCUMENT_FRAME = 82;
    private const int ROLE_DOCUMENT_SPREADSHEET = 92;
    private const int ROLE_DOCUMENT_PRESENTATION = 93;
    private const int ROLE_DOCUMENT_TEXT = 94;
    private const int ROLE_DOCUMENT_WEB = 95;
    private const int ROLE_DOCUMENT_EMAIL = 96;
    
    // Hyperlink
    private const int ROLE_LINK = 88;
    // Image
    private const int ROLE_IMAGE = 27;
    // CheckBox
    private const int ROLE_CHECK_BOX = 7;
    // RadioButtion
    private const int ROLE_RADIO_BUTTON = 44;
    // ComboBox
    private const int ROLE_COMBO_BOX = 11;
    // ListView
    private const int ROLE_LIST = 31;
    // ListViewItem
    private const int ROLE_LIST_ITEM = 32;
    // TreeView
    private const int ROLE_TREE_TABLE = 66;
    // TreeViewItem
    private const int ROLE_TREE = 65;
    // DataGrid
    // DataGridItem
    // TabControl
    
    // TabItem
    private const int ROLE_PAGE_TAB = 37;
    // Table
    private const int ROLE_TABLE = 55;
    // TableRow
    private const int ROLE_TABLE_ROW = 90;
    // Menu
    private const int ROLE_MENU = 33;
    // MenuItem
    private const int ROLE_MENU_ITEM = 35;
    // Slider
    private const int ROLE_SLIDER = 51;
    // ScrollBar
    private const int ROLE_SCROLL_BAR = 48;
    // ProgressBar
    private const int ROLE_PROGRESS_BAR = 42;
    // Panel
    private const int ROLE_PANEL = 39;
    // TopLevel
    private const int ROLE_APPLICATION = 75;
    // Screen
    
    // States
    // Offscreen
    private const int STATE_VISIBLE = 30;
    // Disabled
    private const int STATE_ENABLE = 8;
    // Focused
    private const int STATE_FOCUSED = 12;
    // Selected
    private const int STATE_SELECTED = 23;
    // ReadOnly
    private const int STATE_EDITABLE = 7;
    // Password
    // refer to Role
    
    public delegate void AtspiEventListenerCallback(IntPtr atspiEvent, IntPtr userData);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_init();

    [LibraryImport(LibAtspi)]
    public static partial void atspi_exit();

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_event_listener_new(AtspiEventListenerCallback callbackEvent, IntPtr userData, IntPtr callbackDestroyed);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_event_listener_register(IntPtr listener, IntPtr eventTypeChar, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial void atspi_event_main();
    
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_get_desktop(int i);


    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_name(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_get_role(IntPtr accessible, IntPtr error);
    
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_state_set(IntPtr accessible);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_state_set_contains(IntPtr set, int state);
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
    public static partial IntPtr atspi_accessible_get_accessible_id(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_application(IntPtr accessible);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_component(IntPtr accessible);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_component_get_accessible_at_point(IntPtr component, int x, int y, int coordType, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_component_get_extents(IntPtr component, int coordType, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_text(IntPtr accessible);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_text_get_character_count(IntPtr accessible, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_text_get_text(IntPtr accessible, int start, int end, IntPtr error);
    [LibraryImport("libgobject-2.0.so.0")]
    public static partial IntPtr g_object_ref(IntPtr obj);
    [LibraryImport("libgobject-2.0.so.0")]
    public static partial void g_object_unref(IntPtr obj);

    [LibraryImport("libglib-2.0.so.0")]
    public static partial void g_free(IntPtr mem);
}