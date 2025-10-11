using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Everywhere.Interop;

namespace Everywhere.Linux.Interop;

public class LinuxNativeHelper : INativeHelper
{
    public bool IsInstalled => false; // implement proper detection if needed

    public bool IsAdministrator => false; // Linux elevation detection omitted

    public bool IsUserStartupEnabled { get; set; }

    public bool IsAdministratorStartupEnabled { get; set; }

    public void RestartAsAdministrator()
    {
        // No-op on Linux by default. Could re-exec with sudo if desired.
    }

    public void SetWindowNoFocus(Window window)
    {
        // Platform-specific behavior not implemented. Leave no-op.
    }

    public void SetWindowHitTestInvisible(Window window)
    {
        // Not implemented for minimal Linux support.
    }

    public void SetWindowCornerRadius(Window window, CornerRadius cornerRadius)
    {
        // Avalonia currently doesn't provide a cross-platform way to set window corner radius from code for all platforms.
        // Keep as no-op to satisfy the interface.
    }

    public void HideWindowWithoutAnimation(Window window)
    {
        window.Hide();
    }

    public Task<WriteableBitmap?> GetClipboardBitmapAsync()
    {
        return Task.FromResult<WriteableBitmap?>(null);
    }

    public void ShowDesktopNotification(string message, string? title = null)
    {
        // Try to use libnotify via command line as a best-effort notification
        try
        {
            var args = $"-u normal \"{title ?? "Everywhere"}\" \"{message}\"";
            System.Diagnostics.Process.Start("notify-send", args);
        }
        catch
        {
            // swallow
        }
    }
}
