using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Everywhere.Interop;
using ObjCRuntime;

namespace Everywhere.Mac.Interop;

/// <summary>
/// Provides interop methods for macOS Accessibility API (AXUIElement).
/// </summary>
public partial class AXUIElement : NSObject, IVisualElement
{
    // A unique and stable identifier is harder on macOS. PID + description might be a starting point.
    public string Id => $"{ProcessId}:{DebugDescription}";

    public IVisualElement? Parent => GetWithCache(ref field, () => GetAttributeAsElement(AXAttributeConstants.Parent));

    public IEnumerable<IVisualElement> Children
    {
        get
        {
            if (GetAttribute<NSArray>(AXAttributeConstants.Children) is { } children)
            {
                for (nuint i = 0; i < children.Count; i++)
                {
                    var child = children.GetItem<AXUIElement>(i);
                    if (child is not null)
                    {
                        yield return child;
                    }
                }
            }
        }
    }

    // Sibling navigation is not directly supported by AX API, it must be inferred from parent's children list.
    // Note: This operation is O(N) and involves IPC calls to fetch the children list.
    public IVisualElement? PreviousSibling
    {
        get
        {
            if (Parent is not AXUIElement parent) return null;

            using var children = parent.GetAttribute<NSArray>(AXAttributeConstants.Children);
            if (children is null) return null;

            // AXUIElement equality is handled by the underlying system (CFEqual/isEqual:)
            var index = children.IndexOf(this);
            if (index != nuint.MaxValue && index > 0)
            {
                return children.GetItem<AXUIElement>(index - 1);
            }

            return null;
        }
    }

    public IVisualElement? NextSibling
    {
        get
        {
            if (Parent is not AXUIElement parent) return null;

            using var children = parent.GetAttribute<NSArray>(AXAttributeConstants.Children);
            if (children is null) return null;

            var index = children.IndexOf(this);
            if (index != nuint.MaxValue && index < children.Count - 1)
            {
                return children.GetItem<AXUIElement>(index + 1);
            }

            return null;
        }
    }

    public AXRoleAttribute Role { get; }

    public VisualElementType Type
    {
        get
        {
            // This requires a mapping from AXRole/AXSubrole to VisualElementType
            return Role switch
            {
                AXRoleAttribute.AXStaticText => VisualElementType.Label,
                AXRoleAttribute.AXTextField or AXRoleAttribute.AXTextArea => VisualElementType.TextEdit,
                AXRoleAttribute.AXBrowser => VisualElementType.Document,
                AXRoleAttribute.AXButton or AXRoleAttribute.AXMenuButton or AXRoleAttribute.AXPopUpButton => VisualElementType.Button,
                AXRoleAttribute.AXLink => VisualElementType.Hyperlink,
                AXRoleAttribute.AXImage => VisualElementType.Image,
                AXRoleAttribute.AXCheckBox => VisualElementType.CheckBox,
                AXRoleAttribute.AXRadioButton => VisualElementType.RadioButton,
                AXRoleAttribute.AXComboBox => VisualElementType.ComboBox,
                AXRoleAttribute.AXList => VisualElementType.ListView,
                AXRoleAttribute.AXOutline => VisualElementType.TreeView,
                AXRoleAttribute.AXTabGroup => VisualElementType.TabControl,
                AXRoleAttribute.AXTable or AXRoleAttribute.AXSheet => VisualElementType.Table,
                AXRoleAttribute.AXRow => VisualElementType.TableRow,
                AXRoleAttribute.AXMenuBar or AXRoleAttribute.AXMenu or AXRoleAttribute.AXToolbar => VisualElementType.Menu,
                AXRoleAttribute.AXMenuBarItem or AXRoleAttribute.AXMenuItem => VisualElementType.MenuItem,
                AXRoleAttribute.AXSlider => VisualElementType.Slider,
                AXRoleAttribute.AXScrollBar => VisualElementType.ScrollBar,
                AXRoleAttribute.AXBusyIndicator or
                    AXRoleAttribute.AXProgressIndicator or
                    AXRoleAttribute.AXValueIndicator => VisualElementType.ProgressBar,
                AXRoleAttribute.AXDrawer or
                    AXRoleAttribute.AXGrid or
                    AXRoleAttribute.AXGroup or
                    AXRoleAttribute.AXGrowArea or
                    AXRoleAttribute.AXPage or
                    AXRoleAttribute.AXScrollArea or
                    AXRoleAttribute.AXWebArea => VisualElementType.Panel,
                AXRoleAttribute.AXApplication or AXRoleAttribute.AXWindow => VisualElementType.TopLevel,
                // TODO: ... add more mappings
                _ => VisualElementType.Unknown
            };
        }
    }

    public VisualElementStates States
    {
        get
        {
            var states = VisualElementStates.None;
            if (GetAttribute<NSNumber>(AXAttributeConstants.Enabled)?.BoolValue == false) states |= VisualElementStates.Disabled;
            if (GetAttribute<NSNumber>(AXAttributeConstants.Focused)?.BoolValue == true) states |= VisualElementStates.Focused;
            // TODO: add more state checks?
            return states;
        }
    }

    public string? Name => GetAttribute<NSString>(AXAttributeConstants.Title);

    public PixelRect BoundingRectangle
    {
        get
        {
            try
            {
                var posVal = GetAttribute<AXValue>(AXAttributeConstants.Position);
                var sizeVal = GetAttribute<AXValue>(AXAttributeConstants.Size);

                if (posVal is null || sizeVal is null) return default;

                var pos = posVal.Point;
                var size = sizeVal.Size;
                return new PixelRect((int)pos.X, (int)pos.Y, (int)size.Width, (int)size.Height);
            }
            catch
            {
                return default;
            }
        }
    }

    public int ProcessId
    {
        get
        {
            GetPid(Handle, out var pid);
            return pid;
        }
    }

    // TODO: Get the native window handle if applicable, otherwise return 0.
    public nint NativeWindowHandle => 0;

    private AXUIElement(NativeHandle handle) : base(handle, true)
    {
        var axRole = GetAttribute<NSString>(AXAttributeConstants.Role);
        Role = Enum.TryParse<AXRoleAttribute>(axRole, true, out var role) ? role : AXRoleAttribute.AXUnknown;
    }

    public string? GetText(int maxLength = -1)
    {
        var text = GetAttribute<NSString>(AXAttributeConstants.Value)?.ToString();
        if (string.IsNullOrEmpty(text)) return null;
        return maxLength > 0 && text.Length > maxLength ? text[..maxLength] : text;
    }

    // TODO: Is press enough?
    public void Invoke() => PerformAction(AXAttributeConstants.Press);

    public void SetText(string text)
    {
        using var nsText = new NSString(text);
        SetAttributeValue(Handle, AXAttributeConstants.Value.Handle, nsText.Handle);
    }

    public void SendShortcut(KeyboardShortcut shortcut)
    {
        // This is complex on macOS. It usually involves using CoreGraphics CGEventCreateKeyboardEvent
        // to create and post keyboard events to the process that owns the element.
        throw new NotImplementedException();
    }

    public string? GetSelectionText() => GetAttribute<NSString>(AXAttributeConstants.SelectedText);

    public Task<Bitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        // Use CoreGraphics CGWindowListCreateImage for capturing.
        throw new NotImplementedException();
    }

    #region Helpers

    private T? GetAttribute<T>(NSString attributeName) where T : NSObject
    {
        var error = CopyAttributeValue(Handle, attributeName.Handle, out var value);
        return error == AXError.Success ? Runtime.GetNSObject<T>(value) : null;
    }

    private static IVisualElement? GetWithCache(ref IVisualElement? cache, Func<AXUIElement?> factory)
    {
        if (cache is not null) return cache;
        cache = factory();
        return cache;
    }

    private AXUIElement? GetAttributeAsElement(NSString attributeName)
    {
        var error = CopyAttributeValue(Handle, attributeName.Handle, out var value);
        if (error == AXError.Success && value != 0)
        {
            return new AXUIElement(value);
        }

        return null;
    }

    private void PerformAction(NSString actionName)
    {
        var error = PerformAction(Handle, actionName.Handle);
        if (error != AXError.Success)
        {
            throw new InvalidOperationException($"Failed to perform action {actionName}. Error: {error}");
        }
    }

    private const string AppServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    public static AXUIElement SystemWide { get; } = new(CreateSystemWide());

    public AXUIElement? ElementAtPosition(float x, float y)
    {
        var error = CopyElementAtPosition(Handle, x, y, out var element);
        return error == AXError.Success && element != 0 ? new AXUIElement(element) : null;
    }

    public AXUIElement? ElementByAttributeValue(NSString attributeName)
    {
        var error = CopyAttributeValue(Handle, attributeName.Handle, out var value);
        return error == AXError.Success && value != 0 ? new AXUIElement(value) : null;
    }

    [LibraryImport(AppServices, EntryPoint = "AXUIElementCreateSystemWide")]
    private static partial nint CreateSystemWide();

    [LibraryImport(AppServices, EntryPoint = "AXUIElementCopyElementAtPosition")]
    private static partial AXError CopyElementAtPosition(nint application, float x, float y, out nint element);

    [LibraryImport(AppServices, EntryPoint = "AXUIElementCopyAttributeValue", StringMarshalling = StringMarshalling.Utf8)]
    private static partial AXError CopyAttributeValue(nint element, nint attribute, out nint value);

    [LibraryImport(AppServices, EntryPoint = "AXUIElementPerformAction", StringMarshalling = StringMarshalling.Utf8)]
    private static partial AXError PerformAction(nint element, nint action);

    [LibraryImport(AppServices, EntryPoint = "AXUIElementSetAttributeValue")]
    private static partial AXError SetAttributeValue(nint element, nint attribute, nint value);

    [LibraryImport(AppServices, EntryPoint = "AXUIElementGetPid")]
    private static partial AXError GetPid(nint element, out int pid);

    #endregion
}

/// <summary>
/// Defines common accessibility attribute and action constants.
/// </summary>
public static class AXAttributeConstants
{
    public static readonly NSString Role = new("AXRole");
    public static readonly NSString Subrole = new("AXSubrole");
    public static readonly NSString Parent = new("AXParent");
    public static readonly NSString Children = new("AXChildren");
    public static readonly NSString VisibleChildren = new("AXVisibleChildren");
    public static readonly NSString Title = new("AXTitle");
    public static readonly NSString Description = new("AXDescription");
    public static readonly NSString Value = new("AXValue");
    public static readonly NSString Position = new("AXPosition");
    public static readonly NSString Size = new("AXSize");
    public static readonly NSString Enabled = new("AXEnabled");
    public static readonly NSString Focused = new("AXFocused");
    public static readonly NSString Window = new("AXWindow");
    public static readonly NSString TopLevelUIElement = new("AXTopLevelUIElement");
    public static readonly NSString FocusedUIElement = new("AXFocusedUIElement");
    public static readonly NSString SelectedText = new("AXSelectedText");
    public static readonly NSString NumberOfCharacters = new("AXNumberOfCharacters");

    // Actions
    public static readonly NSString Press = new("AXPress");
}

/// <summary>
/// from NSAccessibilityRoles
/// </summary>
public enum AXRoleAttribute
{
    AXUnknown,
    AXApplication,
    AXBrowser,
    AXBusyIndicator,
    AXButton,
    AXCell,
    AXCheckBox,
    AXColorWell,
    AXColumn,
    AXComboBox,
    AXDisclosureTriangle,
    AXDrawer,
    AXGrid,
    AXGroup,
    AXGrowArea,
    AXHandle,
    AXHelpTag,
    AXImage,
    AXIncrementor,
    AXLayoutArea,
    AXLayoutItem,
    AXLevelIndicator,
    AXLink,
    AXList,
    AXMatte,
    AXMenuBar,
    AXMenuBarItem,
    AXMenuButton,
    AXMenuItem,
    AXMenu,
    AXOutline,
    AXPage,
    AXPopUpButton,
    AXPopover,
    AXProgressIndicator,
    AXRadioButton,
    AXRadioGroup,
    AXRelevanceIndicator,
    AXRow,
    AXRulerMarker,
    AXRuler,
    AXScrollArea,
    AXScrollBar,
    AXSheet,
    AXSlider,
    AXSplitGroup,
    AXSplitter,
    AXStaticText,
    AXSystemWide,
    AXTabGroup,
    AXTable,
    AXTextArea,
    AXTextField,
    AXToolbar,
    AXValueIndicator,
    AXWindow,
    AXWebArea
}

// Define AXError enum based on documentation
public enum AXError
{
    Success = 0,
    Failure = -25200,
    IllegalArgument = -25201,
    InvalidUIElement = -25202,
    InvalidUIElementObserver = -25203,
    CannotComplete = -25204,
    AttributeUnsupported = -25205,
    ActionUnsupported = -25206,
    NotificationUnsupported = -25207,
    NotImplemented = -25208,
    NotificationAlreadyRegistered = -25209,
    NotificationNotRegistered = -25210,
    APIDisabled = -25211,
    NoValue = -25212,
    ParameterizedAttributeUnsupported = -25213,
    NotEnoughPrecision = -25214
}
