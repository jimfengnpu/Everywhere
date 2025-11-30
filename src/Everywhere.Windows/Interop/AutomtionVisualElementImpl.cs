using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Avalonia.Input;
using Avalonia.Threading;
using Everywhere.Extensions;
using Everywhere.Windows.Extensions;
using FlaUI.Core.Definitions;
using IDataObject = System.Windows.IDataObject;
using INPUT = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT;
using KEYBDINPUT = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBDINPUT;
using Everywhere.Interop;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using FlaUI.Core.AutomationElements;
using Avalonia;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia.Media.Imaging;

namespace Everywhere.Windows.Interop;

public partial class VisualElementContext
{
    private class AutomationVisualElementImpl(AutomationElement element, bool windowBarrier) : IVisualElement
    {
        public string Id { get; } = string.Join('.', element.Properties.RuntimeId.ValueOrDefault ?? []);

        public IVisualElement? Parent
        {
            get
            {
                try
                {
                    if (IsTopLevelWindow)
                    {
                        // this is a top level window
                        if (_windowBarrier) return null;

                        var screen = PInvoke.MonitorFromWindow((HWND)NativeWindowHandle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                        return screen == HMONITOR.Null ? null : new ScreenVisualElementImpl(screen);
                    }

                    var parent = TreeWalker.GetParent(_element);
                    return parent is null ? null : new AutomationVisualElementImpl(parent, _windowBarrier);
                }
                catch (COMException)
                {
                    return null;
                }
            }
        }

        public IEnumerable<IVisualElement> Children
        {
            get
            {
                var child = TreeWalker.GetFirstChild(_element);
                while (child is not null)
                {
                    yield return new AutomationVisualElementImpl(child, _windowBarrier);
                    child = TreeWalker.GetNextSibling(child);
                }
            }
        }

        public VisualElementSiblingAccessor SiblingAccessor => new SiblingAccessorImpl(this);

        public VisualElementType Type
        {
            get
            {
                try
                {
                    return _element.Properties.ControlType.ValueOrDefault switch
                    {
                        ControlType.AppBar => VisualElementType.Menu,
                        ControlType.Button => VisualElementType.Button,
                        ControlType.Calendar => VisualElementType.Label,
                        ControlType.CheckBox => VisualElementType.CheckBox,
                        ControlType.ComboBox => VisualElementType.ComboBox,
                        ControlType.DataGrid => VisualElementType.DataGrid,
                        ControlType.DataItem => VisualElementType.DataGridItem,
                        ControlType.Document => VisualElementType.Document,
                        ControlType.Edit => VisualElementType.TextEdit,
                        ControlType.Group => VisualElementType.Panel,
                        ControlType.Header or ControlType.HeaderItem => VisualElementType.TableRow,
                        ControlType.Hyperlink => VisualElementType.Hyperlink,
                        ControlType.Image => VisualElementType.Image,
                        ControlType.List => VisualElementType.ListView,
                        ControlType.ListItem => VisualElementType.ListViewItem,
                        ControlType.Menu or ControlType.MenuBar => VisualElementType.Menu,
                        ControlType.MenuItem => VisualElementType.MenuItem,
                        ControlType.Pane => VisualElementType.TopLevel,
                        ControlType.ProgressBar => VisualElementType.ProgressBar,
                        ControlType.RadioButton => VisualElementType.RadioButton,
                        ControlType.ScrollBar => VisualElementType.ScrollBar,
                        ControlType.SemanticZoom => VisualElementType.ListView,
                        ControlType.Separator => VisualElementType.Unknown,
                        ControlType.Slider or ControlType.Spinner => VisualElementType.Slider,
                        ControlType.SplitButton => VisualElementType.Button,
                        ControlType.StatusBar => VisualElementType.Panel,
                        ControlType.Tab => VisualElementType.TabControl,
                        ControlType.TabItem => VisualElementType.TabItem,
                        ControlType.Table => VisualElementType.Table,
                        ControlType.Text => VisualElementType.Label,
                        ControlType.Thumb => VisualElementType.Slider,
                        ControlType.TitleBar or ControlType.ToolBar or ControlType.ToolTip => VisualElementType.Panel,
                        ControlType.Tree => VisualElementType.TreeView,
                        ControlType.TreeItem => VisualElementType.TreeViewItem,
                        ControlType.Window => VisualElementType.TopLevel,
                        _ => VisualElementType.Unknown
                    };
                }
                catch (COMException)
                {
                    return VisualElementType.Unknown;
                }
            }
        }

        public VisualElementStates States
        {
            get
            {
                try
                {
                    var states = VisualElementStates.None;
                    if (_element.Properties.IsOffscreen.ValueOrDefault) states |= VisualElementStates.Offscreen;
                    if (!_element.Properties.IsEnabled.ValueOrDefault) states |= VisualElementStates.Disabled;
                    if (_element.Properties.HasKeyboardFocus.ValueOrDefault) states |= VisualElementStates.Focused;
                    if (_element.Patterns.SelectionItem.TryGetPattern() is { IsSelected.ValueOrDefault: true })
                        states |= VisualElementStates.Selected;
                    if (_element.Patterns.Value.TryGetPattern() is { IsReadOnly.ValueOrDefault: true }) states |= VisualElementStates.ReadOnly;
                    if (_element.Properties.IsPassword.ValueOrDefault) states |= VisualElementStates.Password;
                    return states;
                }
                catch (COMException)
                {
                    return VisualElementStates.None;
                }
            }
        }

        public string? Name
        {
            get
            {
                try
                {
                    if (_element.Properties.Name.TryGetValue(out var name)) return name;
                    if (_element.Patterns.LegacyIAccessible.TryGetPattern() is { } accessiblePattern) return accessiblePattern.Name;
                    return null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public PixelRect BoundingRectangle
        {
            get
            {
                try
                {
                    return _element.BoundingRectangle.To(r => new PixelRect(
                        r.X,
                        r.Y,
                        r.Width,
                        r.Height));
                }
                catch (COMException)
                {
                    return default;
                }
            }
        }

        public int ProcessId { get; } = element.FrameworkAutomationElement.ProcessId.ValueOrDefault;

        public nint NativeWindowHandle { get; } = element.FrameworkAutomationElement.NativeWindowHandle.ValueOrDefault;

        private readonly AutomationElement _element = element;
        private readonly bool _windowBarrier = windowBarrier;

        public string? GetText(int maxLength = -1)
        {
            try
            {
                if (_element.Patterns.Value.TryGetPattern() is { } valuePattern) return valuePattern.Value;
                if (_element.Patterns.Text.TryGetPattern() is { } textPattern) return textPattern.DocumentRange.GetText(maxLength);
                if (_element.Patterns.LegacyIAccessible.TryGetPattern() is { } accessiblePattern) return accessiblePattern.Value;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void EnsureFocusable()
        {
            try
            {
                _element.Focus();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to focus element before sending shortcut.", ex);
            }
        }

        public void Invoke()
        {
            try
            {
                if (_element.Patterns.Invoke.TryGetPattern() is { } invokePattern)
                {
                    invokePattern.Invoke();
                    return;
                }

                if (_element.Patterns.Toggle.TryGetPattern() is { } togglePattern)
                {
                    togglePattern.Toggle();
                    return;
                }

                if (_element.Patterns.SelectionItem.TryGetPattern() is { } selectionItemPattern)
                {
                    selectionItemPattern.Select();
                    return;
                }

                if (_element.Patterns.ExpandCollapse.TryGetPattern() is { } expandCollapsePattern)
                {
                    var state = expandCollapsePattern.ExpandCollapseState.ValueOrDefault;
                    if (state is ExpandCollapseState.Collapsed or ExpandCollapseState.PartiallyExpanded)
                    {
                        expandCollapsePattern.Expand();
                    }
                    else
                    {
                        expandCollapsePattern.Collapse();
                    }

                    return;
                }

                if (_element.Patterns.LegacyIAccessible.TryGetPattern() is { } legacyPattern)
                {
                    legacyPattern.DoDefaultAction();
                }
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("Failed to invoke the element through UI Automation.", ex);
            }
            catch (Exception ex) when (IsAutomationException(ex))
            {
                throw new InvalidOperationException("Failed to invoke the element through UI Automation.", ex);
            }

            throw new NotSupportedException("The target element does not expose an invoke-capable automation pattern.");
        }

        public void SetText(string text)
        {
            try
            {
                if (_element.Patterns.Value.TryGetPattern() is { } valuePattern)
                {
                    if (valuePattern.IsReadOnly.ValueOrDefault)
                    {
                        throw new InvalidOperationException("The target element is read-only and cannot accept text.");
                    }

                    _element.Focus();
                    new TextBox(_element.FrameworkAutomationElement).Text = text;
                }
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("Failed to set text on the element through UI Automation.", ex);
            }
            catch (Exception ex) when (IsAutomationException(ex))
            {
                throw new InvalidOperationException("Failed to set text on the element through UI Automation.", ex);
            }

            throw new NotSupportedException("The target element does not support programmatic text input.");
        }

        public void SendShortcut(KeyboardShortcut shortcut)
        {
            EnsureFocusable();

            // Use PInvoke.SendInput to send the shortcut to the focused element.
            var inputs = new List<INPUT>();
            if (shortcut.Modifiers.HasFlag(KeyModifiers.Control)) MakeInputs(VIRTUAL_KEY.VK_CONTROL);
            if (shortcut.Modifiers.HasFlag(KeyModifiers.Alt)) MakeInputs(VIRTUAL_KEY.VK_MENU);
            if (shortcut.Modifiers.HasFlag(KeyModifiers.Shift)) MakeInputs(VIRTUAL_KEY.VK_SHIFT);
            if (shortcut.Modifiers.HasFlag(KeyModifiers.Meta)) MakeInputs(VIRTUAL_KEY.VK_LWIN);
            MakeInputs(shortcut.Key.ToVirtualKey());

            var result = PInvoke.SendInput(CollectionsMarshal.AsSpan(inputs), Marshal.SizeOf<INPUT>());
            if (result == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to send keyboard input to the target element.");
            }

            void MakeInputs(VIRTUAL_KEY vk)
            {
                inputs.InsertRange(
                    inputs.Count / 2,
                    [
                        new INPUT
                        {
                            type = INPUT_TYPE.INPUT_KEYBOARD,
                            Anonymous = new INPUT._Anonymous_e__Union
                            {
                                ki = new KEYBDINPUT
                                {
                                    wVk = vk,
                                    dwFlags = 0,
                                }
                            }
                        },
                        new INPUT
                        {
                            type = INPUT_TYPE.INPUT_KEYBOARD,
                            Anonymous = new INPUT._Anonymous_e__Union
                            {
                                ki = new KEYBDINPUT
                                {
                                    wVk = vk,
                                    dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                                }
                            }
                        },
                    ]);
            }
        }

        public string? GetSelectionText()
        {
            try
            {
                // 1) Prefer UIA TextPattern selection text
                if (_element.Patterns.Text.TryGetPattern() is { } textPattern)
                {
                    var ranges = textPattern.GetSelection();
                    if (ranges is { Length: > 0 })
                    {
                        var selected = string.Join(null, ranges.Select(r => r.GetText(-1)));
                        if (!string.IsNullOrEmpty(selected))
                            return selected;
                    }
                }

                // 2) Fallback to SelectionItemPattern (if selected, return element's text)
                if (_element.Patterns.SelectionItem.TryGetPattern() is { } selectionItemPattern)
                {
                    if (selectionItemPattern.IsSelected.ValueOrDefault)
                    {
                        var v = GetText();
                        if (!string.IsNullOrEmpty(v))
                            return v;
                    }
                }

                // TODO: Following method takes no effect QAQ
                // 3) Last resort: send WM_COPY to the focused child window of target thread, then wait for clipboard update
                if (!TryGetWindow(_element, out var topLevel) || topLevel == 0)
                    return null;

                var hTop = (HWND)topLevel;

                // Resolve the real focused child HWND in the target GUI thread
                var target = hTop;
                var targetTid = PInvoke.GetWindowThreadProcessId(hTop);
                var currentTid = PInvoke.GetCurrentThreadId();
                var attached = false;
                try
                {
                    attached = PInvoke.AttachThreadInput(currentTid, targetTid, true);
                    var hFocus = PInvoke.GetFocus();
                    if (hFocus != HWND.Null)
                        target = hFocus;
                }
                finally
                {
                    if (attached)
                        _ = PInvoke.AttachThreadInput(currentTid, targetTid, false);
                }

                // Read clipboard text (best effort)
                string? result = null;
                Dispatcher.UIThread.Invoke(() =>
                {
                    // Backup current clipboard (best effort, avoid user-visible side effects)
                    IDataObject? backup = null;
                    try
                    {
                        // backup = Clipboard.GetDataObject();
                    }
                    catch
                    {
                        /* ignore */
                    }

                    // Arm the clipboard listener before sending WM_COPY to avoid race
                    var listener = ClipboardListener.Shared;
                    listener.BeginWait();

                    // Ask target control to copy selection without simulating Ctrl+C
                    PInvoke.SendMessage(target, (uint)WINDOW_MESSAGE.WM_COPY, 0, 0);

                    // Wait for WM_CLIPBOARDUPDATE (timeout ~50ms)
                    if (!listener.WaitNextUpdate(50)) return;

                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            result = Clipboard.GetText();
                        }
                    }
                    catch
                    {
                        /* ignore */
                    }

                    // Restore clipboard
                    if (backup != null)
                    {
                        try
                        {
                            Clipboard.SetDataObject(backup, true);
                        }
                        catch
                        {
                            /* ignore */
                        }
                    }
                });

                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch
            {
                return null;
            }
        }

        // BUG: For a minimized window, the captured image is buggy (but child elements are fine).
        public Task<Bitmap> CaptureAsync(CancellationToken cancellationToken)
        {
            var rect = BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
                throw new InvalidOperationException("Cannot capture an element with zero width or height.");

            if (!TryGetWindow(_element, out var hWnd) ||
                (hWnd = PInvoke.GetAncestor((HWND)hWnd, GET_ANCESTOR_FLAGS.GA_ROOTOWNER)) == 0)
                throw new InvalidOperationException("Cannot capture an element without a valid window handle.");

            if (!PInvoke.GetWindowRect((HWND)hWnd, out var windowRect))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return Direct3D11ScreenCapture.CaptureAsync(
                hWnd,
                new PixelRect(
                    rect.X - windowRect.X,
                    rect.Y - windowRect.Y,
                    rect.Width,
                    rect.Height),
                cancellationToken);
        }

        #region Interop

        private static bool TryGetWindow(AutomationElement? element, out nint hWnd)
        {
            while (element != null)
            {
                if (element.FrameworkAutomationElement.NativeWindowHandle.TryGetValue(out hWnd))
                {
                    return true;
                }

                element = TreeWalker.GetParent(element);
            }

            hWnd = 0;
            return false;
        }

        /// <summary>
        ///     Determines if the current element is a top-level window in a Win32 context.
        /// </summary>
        /// <remarks>
        ///     e.g. A control inside a window or a non-win32 element will return false.
        /// </remarks>
        public bool IsTopLevelWindow =>
            NativeWindowHandle != IntPtr.Zero &&
            PInvoke.GetAncestor((HWND)NativeWindowHandle, GET_ANCESTOR_FLAGS.GA_ROOTOWNER) == NativeWindowHandle;

        #endregion


        public override bool Equals(object? obj)
        {
            if (obj is not AutomationVisualElementImpl other) return false;
            return Id == other.Id;
        }

        public override int GetHashCode() => Id.GetHashCode();

        public override string ToString() => $"({Id}) [{_element.ControlType}] {Name} - {GetText(128)}";


        private sealed class SiblingAccessorImpl(AutomationVisualElementImpl visualElement) : VisualElementSiblingAccessor
        {
            protected override IEnumerator<IVisualElement> CreateForwardEnumerator()
            {
                var sibling = TreeWalker.GetNextSibling(visualElement._element);
                while (sibling is not null)
                {
                    yield return new AutomationVisualElementImpl(sibling, visualElement._windowBarrier);
                    sibling = TreeWalker.GetNextSibling(sibling);
                }
            }

            protected override IEnumerator<IVisualElement> CreateBackwardEnumerator()
            {
                var sibling = TreeWalker.GetPreviousSibling(visualElement._element);
                while (sibling is not null)
                {
                    yield return new AutomationVisualElementImpl(sibling, visualElement._windowBarrier);
                    sibling = TreeWalker.GetPreviousSibling(sibling);
                }
            }
        }
    }
}