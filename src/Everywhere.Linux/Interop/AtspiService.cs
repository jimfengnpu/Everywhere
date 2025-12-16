using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using CSharpMath;
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
    private readonly ILinuxWindowBackend _windowBackend;
    private readonly ConcurrentDictionary<IntPtr, AtspiVisualElement> _cachedElement = new();
    private readonly ILogger<AtspiService> _logger = ServiceLocator.Resolve<ILogger<AtspiService>>();
    private readonly AtspiEventListenerCallback _eventCallback;
    private readonly Lock _focusLock = new();
    
    private IntPtr _eventListener;
    private IntPtr _focusedElement;

    public AtspiService(ILinuxWindowBackend backend)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AT_SPI_BUS")))
            Environment.SetEnvironmentVariable("AT_SPI_BUS", "session");

        if (atspi_init() < 0)
            throw new InvalidOperationException("Failed to initialize AT-SPI");
        _eventCallback = OnEvent;
        _eventListener = atspi_event_listener_new(_eventCallback, IntPtr.Zero, IntPtr.Zero);
        atspi_event_listener_register(
            _eventListener,
            Marshal.StringToCoTaskMemUTF8("object:state-changed:focused"),
            IntPtr.Zero);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            atspi_event_main();
        });
        _windowBackend = backend;
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
        if (_eventListener != IntPtr.Zero)
        {
            atspi_event_listener_deregister(
                _eventListener,
                Marshal.StringToCoTaskMemUTF8("object:state-changed:focused"),
                IntPtr.Zero);
            _eventListener = IntPtr.Zero;
        }
        atspi_exit();
    }

    private void OnEvent(IntPtr atspiEventPtr, IntPtr userData)
    {
        try
        {
            var ev = Marshal.PtrToStructure<AtspiEvent>(atspiEventPtr);
            var eventType = Marshal.PtrToStringAnsi(ev.type) ?? string.Empty;
            if (eventType.Contains("focused") && ev.source != IntPtr.Zero)
            {
                lock (_focusLock)
                {
                    if (ev.detail1 == 1) // focus in
                    {
                        var element = ev.source;
                        if (_focusedElement != IntPtr.Zero)
                        {
                            g_object_unref(_focusedElement);
                        }
                        // var appName = Process.GetProcessById(ElementPid(element)).ProcessName;
                        // _logger.LogInformation("Focus in: {app} - {Name}", appName, ElementName(element));
                        _focusedElement = element;
                        g_object_ref(_focusedElement);
                    }
                    else
                    {
                        if (_focusedElement != IntPtr.Zero)
                        {
                            g_object_unref(_focusedElement);
                        }
                        _focusedElement = IntPtr.Zero;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnEvent failed: {Message}", ex.Message);
        }
    }

    private static string? ElementName(IntPtr elem)
    {
        try
        {
            var namePtr = atspi_accessible_get_name(elem, IntPtr.Zero);
            if (namePtr == IntPtr.Zero)
            {
                return null;
            }
            var name = Marshal.PtrToStringUTF8(namePtr);
            g_free(namePtr);
            return name;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static int ElementPid(IntPtr elem)
    {
        var pid = atspi_accessible_get_process_id(elem, IntPtr.Zero);
        return pid;
    }

    private static PixelRect ElementBounds(IntPtr elem)
    {
        var rectPtr = atspi_component_get_extents(elem, (int)AtspiCoordType.Screen, IntPtr.Zero);
        var rect = Marshal.PtrToStructure<AtspiRect>(rectPtr);
        g_free(rectPtr);
        return new PixelRect(rect.x, rect.y, rect.width, rect.height);
    }

    private static VisualElementStates ElementState(IntPtr elem)
    {
        try
        {
            var states = VisualElementStates.None;
            var elemStateset = atspi_accessible_get_state_set(elem);
            if ((atspi_state_set_contains(elemStateset, (int)AtspiState.Visible) == 0)
                || (atspi_state_set_contains(elemStateset, (int)AtspiState.Showing) == 0))
                states |= VisualElementStates.Offscreen;
            if (atspi_state_set_contains(elemStateset, (int)AtspiState.Enable) == 0) states |= VisualElementStates.Disabled;
            if (atspi_state_set_contains(elemStateset, (int)AtspiState.Focused) == 1) states |= VisualElementStates.Focused;
            if (atspi_state_set_contains(elemStateset, (int)AtspiState.Selected) == 1) states |= VisualElementStates.Selected;
            if (atspi_state_set_contains(elemStateset, (int)AtspiState.Editable) == 0) states |= VisualElementStates.ReadOnly;
            g_object_unref(elemStateset);
            if (atspi_accessible_get_role(elem, IntPtr.Zero) == (int)AtspiRole.PasswordText)
                states |= VisualElementStates.Password;
            return states;
        }
        catch (COMException)
        {
            return VisualElementStates.None;
        }
    }

    private IntPtr GArrayIndex(IntPtr array, int index)
    {
        // note: in glib garray structure, data pointer is always located at the beginning
        // see glib/garray.h
        IntPtr data = Marshal.ReadIntPtr(array);
        return Marshal.ReadIntPtr(data, IntPtr.Size * index);
    }

    private int GArrayLength(IntPtr array)
    {
        // garray structure length is located after data pointer
        return Marshal.ReadInt32(array, IntPtr.Size);
    }

    private IntPtr TryMatchRelationWindow(IntPtr window, bool down)
    {
        var array = atspi_accessible_get_relation_set(window, IntPtr.Zero);
        var count = GArrayLength(array);
        for (var i = 0; i < count; i++)
        {
            var relation = GArrayIndex(array, i);
            if (relation != IntPtr.Zero)
            {
                var type = atspi_relation_get_relation_type(relation);
                var nTarget = atspi_relation_get_n_targets(relation);
                if (((type == (int)AtspiRelationType.Embeds || type == (int)AtspiRelationType.SubwindowOf && down)
                        || (type == (int)AtspiRelationType.EmbeddedBy && (!down)))
                    && nTarget == 1)
                {
                    var target = atspi_relation_get_target(relation, 0);
                    if (target != IntPtr.Zero && ElementVisible(target))
                    {
                        return target;
                    }
                }
            }
        }
        return IntPtr.Zero;
    }

    private bool ElementVisible(IntPtr elem, bool includeApp = false)
    {
        if (ElementState(elem).HasFlag(VisualElementStates.Offscreen)) return false;
        if (includeApp && atspi_accessible_is_application(elem) != 0)
        {
            return true;
        }
        if (atspi_accessible_is_component(elem) == 0) return false;
        var rect = ElementBounds(elem);
        return rect is { Height: > 0, Width: > 0 };
    }

    private IEnumerable<AtspiVisualElement> ElementChildren(IntPtr elem)
    {
        var relatedSubWindow = TryMatchRelationWindow(elem, true);
        if (relatedSubWindow != IntPtr.Zero && ElementVisible(relatedSubWindow, true))
        {
            var sub = GetAtspiVisualElement(() => relatedSubWindow);
            if (sub != null)
            {
                yield return sub;
            }
        }
        else
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
    }

    private AtspiVisualElement? AtspiElementFromPoint(AtspiVisualElement? parent, PixelPoint point, bool root = false)
    {

        parent ??= GetAtspiVisualElement(() => atspi_get_desktop(0));
        if (parent is null)
        {
            return null;
        }
        if (!root && !ElementVisible(parent._element, true))
        {
            return null;
        }
        _logger.LogInformation("> {Name} {States}", parent.Name, parent.States);
        var rect = parent.BoundingRectangle;
        if (rect is { Height: > 0, Width: > 0 } && !rect.Contains(point))
        {
            return null;
        }
        foreach (var child in ElementChildren(parent._element)
                     .OrderByDescending(child => child.Order))
        {
            var found = AtspiElementFromPoint(child, point);
            if (found != null)
            {
                return found;
            }
        }

        if (rect is { Height: > 0, Width: > 0 } && rect.Contains(point) && ElementVisible(parent._element))
        {
            return parent;
        }

        return null;
    }

    private AtspiVisualElement? AtspiAppElementByPid(int pid)
    {
        var root = GetAtspiVisualElement(() => atspi_get_desktop(0));
        if (root is null)
        {
            return null;
        }
        foreach (var child in ElementChildren(root._element)
                     .OrderByDescending(child => child.Order))
        {
            if (child.ProcessId == pid)
            {
                return child;
            }
        }
        return null;
    }

    public IVisualElement? ElementFromPoint(PixelPoint point, int pid)
    {
        CheckInitialized();
        var app = AtspiAppElementByPid(pid);
        if (app == null)
        {
            _logger.LogInformation("App {pid} do not support At-SPI", pid);
            return null;
        }
        var elem = AtspiElementFromPoint(app, point, true);
        if (elem == null)
        {
            _logger.LogWarning("AtspiElementFromPoint {Point} not found", point);
        }
        else
        {
            _logger.LogInformation(
                "AtspiElementFromPoint {Point} found: {Name}, {Rect}",
                point,
                elem.Name,
                elem.BoundingRectangle);
        }
        return elem;
    }

    public IVisualElement? ElementFocused()
    {
        try
        {
            CheckInitialized();
            return GetAtspiVisualElement(() => _focusedElement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AtspiFocusedElement failed");
            return null;
        }
    }

    private class AtspiVisualElement(AtspiService atspi, IntPtr elementPtr)
        : IVisualElement, IDisposable
    {
        public readonly IntPtr _element = g_object_ref(elementPtr);
        private readonly List<IntPtr> _cachedAccessibleChildren = [];
        private bool _childrenCached;
        private bool _disposed;
        private readonly Lock _childrenLoading = new();

        public void Dispose()
        {
            if (!_disposed && _element != IntPtr.Zero)
            {
                g_object_unref(_element);
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
                if (_element == IntPtr.Zero) return null;
                var parent = atspi.TryMatchRelationWindow(_element, false);
                if (parent == IntPtr.Zero)
                {
                    parent = atspi_accessible_get_parent(_element, IntPtr.Zero);
                }
                if (parent == IntPtr.Zero) return null;
                return atspi_accessible_is_application(parent) != 0 ?
                    null :
                    atspi.GetAtspiVisualElement(() => parent);
            }
        }

        private int IndexInParent => _element == IntPtr.Zero ? 0 : atspi_accessible_get_index_in_parent(_element, IntPtr.Zero);

        private void EnsureChildCached()
        {
            lock (_childrenLoading)
            {
                if (!_childrenCached)
                {
                    _cachedAccessibleChildren.Clear();
                    foreach (var elem in atspi.ElementChildren(_element))
                    {
                        _cachedAccessibleChildren.Add(elem._element);
                    }
                    _childrenCached = true;
                }
            }
        }

        public IEnumerable<IVisualElement> Children
        {
            get
            {
                if (_element == IntPtr.Zero)
                    yield break;
                EnsureChildCached();
                foreach (var child in _cachedAccessibleChildren)
                {
                    yield return atspi._cachedElement[child];
                }
            }
        }

        private class AtspiSiblingAccessor(
            AtspiService atspi,
            AtspiVisualElement? parent,
            AtspiVisualElement element
        ) : VisualElementSiblingAccessor
        {
            private int _index = 0;

            protected override void EnsureResources()
            {
                base.EnsureResources();
                parent?.EnsureChildCached();
                if (parent == null) return;
                int index = 0;
                foreach (var child in parent._cachedAccessibleChildren)
                {
                    if (child == element._element)
                    {
                        _index = index;
                    }
                    index++;
                }
            }

            protected override IEnumerator<IVisualElement> CreateForwardEnumerator()
            {
                if (parent == null)
                {
                    yield break;
                }
                for (var i = _index + 1; i < parent._cachedAccessibleChildren.Count; i++)
                {
                    yield return atspi._cachedElement[parent._cachedAccessibleChildren[i]];
                }
            }

            protected override IEnumerator<IVisualElement> CreateBackwardEnumerator()
            {
                if (parent == null)
                {
                    yield break;
                }
                for (var i = _index - 1; i >= 0; i--)
                {
                    yield return atspi._cachedElement[parent._cachedAccessibleChildren[i]];
                }
            }
        }

        public VisualElementSiblingAccessor SiblingAccessor => new AtspiSiblingAccessor(atspi, (AtspiVisualElement?)Parent, this);

        public VisualElementType Type
        {
            get
            {
                try
                {
                    var roleEnum = (AtspiRole)atspi_accessible_get_role(_element, IntPtr.Zero);
                    return roleEnum switch
                    {
                        AtspiRole.Application or AtspiRole.Frame or AtspiRole.Canvas => VisualElementType.TopLevel,
                        AtspiRole.Button or AtspiRole.SpinButton or AtspiRole.ToggleButton or AtspiRole.PushButton => VisualElementType.Button,
                        AtspiRole.CheckBox or AtspiRole.Switch => VisualElementType.CheckBox,
                        AtspiRole.ComboBox => VisualElementType.ComboBox,
                        AtspiRole.DocumentEmail or AtspiRole.DocumentFrame or AtspiRole.DocumentPresentation or AtspiRole.DocumentSpreadsheet or
                            AtspiRole.DocumentText or AtspiRole.DocumentWeb or AtspiRole.HtmlContainer or AtspiRole.Paragraph or AtspiRole.Form or
                            AtspiRole.DescriptionValue => VisualElementType.Document,
                        AtspiRole.Entry or AtspiRole.Editbar or AtspiRole.PasswordText => VisualElementType.TextEdit,
                        AtspiRole.Image or AtspiRole.DesktopIcon or AtspiRole.Icon => VisualElementType.Image,
                        AtspiRole.Label or AtspiRole.Text or AtspiRole.Header or AtspiRole.Footer or AtspiRole.Caption or AtspiRole.Comment or
                            AtspiRole.DescriptionTerm or AtspiRole.Footnote => VisualElementType.Label,
                        AtspiRole.Link => VisualElementType.Hyperlink,
                        AtspiRole.List or AtspiRole.ListBox or AtspiRole.DescriptionList => VisualElementType.ListView,
                        AtspiRole.ListItem => VisualElementType.ListViewItem,
                        AtspiRole.Menu => VisualElementType.Menu,
                        AtspiRole.MenuItem or AtspiRole.CheckMenuItem or AtspiRole.TearoffMenuItem => VisualElementType.MenuItem,
                        AtspiRole.PageTab => VisualElementType.TabItem,
                        AtspiRole.Panel => VisualElementType.Panel,
                        AtspiRole.ProgressBar => VisualElementType.ProgressBar,
                        AtspiRole.RadioButton => VisualElementType.RadioButton,
                        AtspiRole.ScrollBar => VisualElementType.ScrollBar,
                        AtspiRole.Slider => VisualElementType.Slider,
                        AtspiRole.Table => VisualElementType.Table,
                        AtspiRole.TableRow => VisualElementType.TableRow,
                        AtspiRole.Tree => VisualElementType.TreeViewItem,
                        AtspiRole.TreeTable => VisualElementType.TreeView,
                        _ => VisualElementType.Unknown
                    };
                }
                catch (COMException)
                {
                    return VisualElementType.Unknown;
                }
            }
        }

        public VisualElementStates States => ElementState(_element);

        public string? Name => ElementName(_element);

        public string Id
        {
            get
            {
                var idPtr = atspi_accessible_get_accessible_id(_element, IntPtr.Zero);
                var idStr = Marshal.PtrToStringUTF8(idPtr) ?? "";
                if (idPtr == IntPtr.Zero || idStr.IsEmpty())
                {
                    return _element.ToString("X");
                }
                g_free(idPtr);
                return idStr;
            }
        }

        public int ProcessId => ElementPid(_element);

        private IVisualElement? OwnerWindow => atspi._windowBackend.GetWindowElementByPid(ProcessId);

        public nint NativeWindowHandle => OwnerWindow?.NativeWindowHandle ?? IntPtr.Zero;

        public string? GetText(int maxLength = -1)
        {
            if (atspi_accessible_is_text(_element) == 0) return null;
            var objTextCount = atspi_accessible_get_child_count(_element, IntPtr.Zero);
            if (objTextCount > 0)
            {
                // in this case, libatspi return objTextCount char “obj char” (U+FFFC), we just simply return null
                return null;
            }
            var count = atspi_text_get_character_count(_element, IntPtr.Zero);
            var rawText = atspi_text_get_text(_element, 0, maxLength == -1 ? count : maxLength, IntPtr.Zero);
            if (rawText == IntPtr.Zero)
            {
                return null;
            }
            var text = Marshal.PtrToStringUTF8(rawText);
            g_free(rawText);
            return text;
        }

        public string? GetSelectionText()
        {
            if (atspi_accessible_is_text(_element) == 0) return null;
            var nSelections = atspi_text_get_n_selections(_element, IntPtr.Zero);
            String selected = "";
            for (var i = 0; i < nSelections; i++)
            {
                var rawRange = atspi_text_get_selection(_element, i, IntPtr.Zero);
                var range = Marshal.PtrToStructure<AtspiRange>(rawRange);
                var rawText = atspi_text_get_text(_element, range.start, range.end, IntPtr.Zero);
                if (rawText == IntPtr.Zero)
                {
                    return null;
                }
                var text = Marshal.PtrToStringUTF8(rawText);
                selected += text;
                g_free(rawText);
                g_free(rawRange);
            }
            return selected;
        }

        public void Invoke()
        {
            if (atspi_accessible_is_action(_element) == 0)
            {
                return;
            }
            var nAction = atspi_action_get_n_actions(_element, IntPtr.Zero);
            if (nAction == 0)
            {
                return;
            }
            atspi_action_do_action(_element, 0, IntPtr.Zero);
        }

        public void SetText(string text)
        {
            if (States.HasFlag(VisualElementStates.ReadOnly))
            {
                return;
            }
            if (atspi_accessible_is_editable_text(_element) == 0)
            {
                return;
            }
            atspi_editable_text_set_text_contents(_element, Marshal.StringToCoTaskMemUTF8(text), IntPtr.Zero);
        }

        public void SendShortcut(KeyboardShortcut shortcut)
        {
            if (atspi_accessible_is_component(_element) == 0)
            {
                return;
            }
            if (atspi_component_grab_focus(_element, IntPtr.Zero) == 0)
            {
                return;
            }
            atspi._windowBackend.SendKeyboardShortcut(shortcut);
        }

        private static int LayerOrder(AtspiLayer layer)
        {
            return layer switch
            {
                AtspiLayer.Background => 1,
                AtspiLayer.Window => 2,
                AtspiLayer.Mdi => 3,
                AtspiLayer.Canvas => 4,
                AtspiLayer.Widget => 5,
                AtspiLayer.Popup => 6,
                AtspiLayer.Overlay => 7,
                _ => 0
            };
        }

        public int Order
        {
            get
            {
                var layer = atspi_component_get_layer(_element, IntPtr.Zero);
                var z = atspi_component_get_mdi_z_order(_element, IntPtr.Zero);
                return LayerOrder((AtspiLayer)layer) * 256 + z * 16 + IndexInParent;
            }
        }

        public PixelRect BoundingRectangle
        {
            get
            {
                if (atspi_accessible_is_component(_element) == 0)
                {
                    var unionRect = new PixelRect();
                    foreach (var child in atspi.ElementChildren(_element))
                    {
                        unionRect.Union(child.BoundingRectangle);
                    }
                    return unionRect;
                }
                var rect = ElementBounds(_element);
                atspi._logger.LogDebug(
                    "Element {Name} BoundingRectangle: {X},{Y} - {W}x{H}",
                    Name,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height);
                return rect;
            }
        }

        public Task<Bitmap> CaptureAsync(CancellationToken cancellationToken)
        {
            var rect = BoundingRectangle;
            if (OwnerWindow != null)
            {
                rect = rect.Translate(-(PixelVector)OwnerWindow.BoundingRectangle.Position);
            }
            return Task.FromResult(atspi._windowBackend.Capture(this, rect));
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
            _logger.LogInformation("Element add: {Name}({Type})[{States}]", elem.Name, elem.Type, elem.States);
            _cachedElement[element] = elem;
            return elem;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private const string LibAtspi = "libatspi.so.0";

    public enum AtspiCoordType
    {
        Screen = 0,
        Window = 1,
        Parent = 2
    }

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
    public enum AtspiRole
    {
        Invalid = 0,
        // Label(Text)
        Label = 29,
        Text = 61,
        Header = 71,
        Footer = 72,
        Caption = 81,
        Comment = 97,
        DescriptionTerm = 122,
        Footnote = 124,
        // Button
        Button = 43,
        SpinButton = 52,
        ToggleButton = 62,
        PushButton = 129,
        // TextEdit
        Entry = 79, // ATSPI_STATE_EDITABLE else Text Role
        Editbar = 77,
        PasswordText = 40,
        // Document
        DocumentFrame = 82,
        DocumentSpreadsheet = 92,
        DocumentPresentation = 93,
        DocumentText = 94,
        DocumentWeb = 95,
        DocumentEmail = 96,
        HtmlContainer = 25,
        Paragraph = 73,
        Form = 87,
        DescriptionValue = 123,
        // Hyperlink
        Link = 88,
        // Image
        Image = 27,
        DesktopIcon = 13,
        Icon = 26,
        // CheckBox
        CheckBox = 7,
        Switch = 130,
        // RadioButton
        RadioButton = 44,
        // ComboBox
        ComboBox = 11,
        // ListView
        List = 31,
        ListBox = 98,
        DescriptionList = 121,
        // ListViewItem
        ListItem = 32,
        // TreeView
        TreeTable = 66,
        // TreeViewItem
        Tree = 65,
        // TabItem
        PageTab = 37,
        // Table
        Table = 55,
        // TableRow
        TableRow = 90,
        // Menu
        Menu = 33,
        // MenuItem
        MenuItem = 35,
        CheckMenuItem = 8,
        TearoffMenuItem = 59,
        // Slider
        Slider = 51,
        // ScrollBar
        ScrollBar = 48,
        // ProgressBar
        ProgressBar = 42,
        // Panel
        Panel = 39,
        // TopLevel
        Application = 75,
        Frame = 23,
        Canvas = 6
    }

    // States
    public enum AtspiState
    {
        // Offscreen
        Showing = 25,
        Visible = 30,
        // Disabled
        Enable = 8,
        // Focused
        Focused = 12,
        // Selected
        Selected = 23,
        // ReadOnly
        Editable = 7
    }
    // Password
    // refer to Role

    // Relation Type
    public enum AtspiRelationType
    {
        SubwindowOf = 12,
        Embeds = 13,
        EmbeddedBy = 14
    }

    // Component Layer
    public enum AtspiLayer
    {
        Invalid = 0,
        Background = 1,
        Canvas = 2,
        Widget = 3,
        Mdi = 4,
        Popup = 5,
        Overlay = 6,
        Window = 7
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AtspiEvent
    {
        public IntPtr type; // char*
        public IntPtr source; // AtspiAccessible*
        public int detail1;
        public int detail2;
        // GValue 
        public IntPtr anyValueType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] anyValueData;
        public IntPtr sender;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AtspiRange
    {
        public int start;
        public int end;
    }

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
    public static partial int atspi_event_listener_deregister(IntPtr listener, IntPtr eventTypeChar, IntPtr error);

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
    public static partial IntPtr atspi_accessible_get_relation_set(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_relation_get_n_targets(IntPtr relation);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_relation_get_relation_type(IntPtr relation);

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_relation_get_target(IntPtr accessible, int i);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_application(IntPtr accessible);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_component(IntPtr accessible);

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_component_get_accessible_at_point(IntPtr component, int x, int y, int coordType, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_component_get_layer(IntPtr component, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_component_get_extents(IntPtr component, int coordType, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial short atspi_component_get_mdi_z_order(IntPtr component, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_component_grab_focus(IntPtr component, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_text(IntPtr accessible);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_text_get_character_count(IntPtr text, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_text_get_text(IntPtr text, int start, int end, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_text_get_n_selections(IntPtr text, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_text_get_selection(IntPtr text, int selectionNum, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_editable_text(IntPtr accessible);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_editable_text_set_text_contents(IntPtr editable, IntPtr text, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_action(IntPtr accessible);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_action_get_n_actions(IntPtr action, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_action_do_action(IntPtr action, int i, IntPtr error);

    [LibraryImport("libgobject-2.0.so.0")]
    public static partial IntPtr g_object_ref(IntPtr obj);

    [LibraryImport("libgobject-2.0.so.0")]
    public static partial void g_object_unref(IntPtr obj);

    [LibraryImport("libglib-2.0.so.0")]
    public static partial void g_free(IntPtr mem);
}