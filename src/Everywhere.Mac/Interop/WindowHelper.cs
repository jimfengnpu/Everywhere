using Avalonia.Controls;
using Everywhere.Interop;

namespace Everywhere.Mac.Interop;

public class WindowHelper : IWindowHelper
{
    /// <summary>
    /// Sets whether the window can become the key window (i.e., receive keyboard focus).
    /// </summary>
    /// <param name="window">The Avalonia window.</param>
    /// <param name="focusable">True to allow focus, false to prevent it.</param>
    public void SetFocusable(Window window, bool focusable)
    {
        if (GetNativeWindow(window) is not { } nativeWindow) return;

        // On macOS, the equivalent of a non-focusable window is often a panel that doesn't activate.
        // We can achieve this by modifying the window's style mask.
        // However, a simpler and less intrusive way is to prevent it from becoming the key window.
        // This requires subclassing NSWindow, which is complex with Avalonia.
        // A pragmatic approach is to set its level to one for utility windows, which often don't take focus.
        // The most direct way is to use a private API or subclass, but for now, we can try setting the window level.
        // A more robust solution might involve changing the CollectionBehavior.
        if (focusable)
        {
            // Restore default behavior. This is hard without knowing the original state.
            // For now, we assume it's a normal window.
            nativeWindow.Level = NSWindowLevel.Normal;
        }
        else
        {
            // Setting a high window level prevents it from becoming the main or key window.
            // This is similar to WS_EX_NOACTIVATE.
            nativeWindow.Level = NSWindowLevel.Floating;
        }
    }

    /// <summary>
    /// Sets whether the window is transparent to mouse events.
    /// </summary>
    /// <param name="window">The Avalonia window.</param>
    /// <param name="visible">True to make it receive mouse events, false to let them pass through.</param>
    public void SetHitTestVisible(Window window, bool visible)
    {
        if (GetNativeWindow(window) is not { } nativeWindow) return;

        // This is the direct equivalent of WS_EX_TRANSPARENT on Windows.
        nativeWindow.IgnoresMouseEvents = !visible;
    }

    /// <summary>
    /// Gets the effective visibility of the window, considering its occlusion state.
    /// </summary>
    /// <param name="window">The Avalonia window.</param>
    /// <returns>True if the window is truly visible on screen.</returns>
    public bool GetEffectiveVisible(Window window)
    {
        if (GetNativeWindow(window) is not { } nativeWindow) return window.IsVisible;

        // NSWindow.IsVisible checks if the window is on-screen.
        // NSWindow.OcclusionState tells us if it's obscured by other windows.
        // A window is effectively visible if it's marked as visible and not fully occluded.
        var isVisible = nativeWindow.IsVisible;
        var isOccluded = (nativeWindow.OcclusionState & NSWindowOcclusionState.Visible) == 0;

        return isVisible && !isOccluded;
    }

    /// <summary>
    /// Hides or shows the window from the user's view without destroying it.
    /// macOS doesn't have a direct "Cloak" concept like DWM.
    /// The closest equivalent is hiding the window and managing its space behavior.
    /// </summary>
    /// <param name="window">The Avalonia window.</param>
    /// <param name="cloaked">True to hide (cloak), false to show (uncloak).</param>
    public void SetCloaked(Window window, bool cloaked)
    {
        if (GetNativeWindow(window) is not { } nativeWindow) return;

        if (cloaked)
        {
            // Hide the window and ensure it's not in the window cycle (Cmd+Tab).
            nativeWindow.CollectionBehavior |= NSWindowCollectionBehavior.IgnoresCycle;
            window.Hide();
        }
        else
        {
            // Show the window, make it the frontmost, and restore its cycle behavior.
            window.Show();
            nativeWindow.CollectionBehavior &= ~NSWindowCollectionBehavior.IgnoresCycle;
            nativeWindow.MakeKeyAndOrderFront(null);
            NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
        }
    }

    /// <summary>
    /// Checks if the window has any open modal dialogs.
    /// </summary>
    /// <param name="window">The Avalonia window.</param>
    /// <returns>True if a modal dialog is active for this window.</returns>
    public bool AnyModelDialogOpened(Window window)
    {
        if (GetNativeWindow(window) is not { } nativeWindow) return false;

        // NSApplication.SharedApplication.ModalWindow returns the current modal window.
        // We check if that modal window's sheet parent is our window.
        var modalWindow = NSApplication.SharedApplication.ModalWindow;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (modalWindow is not null)
        {
            // If a sheet is presented, its Window is the sheet itself, and SheetParent is the owner.
            if (modalWindow.SheetParent.Equals(nativeWindow))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the native NSWindow from an Avalonia Window.
    /// </summary>
    private static NSWindow? GetNativeWindow(Window window)
    {
        return window.TryGetPlatformHandle()?.Handle is { } handle
            ? ObjCRuntime.Runtime.GetNSObject<NSWindow>(handle)
            : null;
    }
}
