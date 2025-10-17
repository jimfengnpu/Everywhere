using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Everywhere.Common;
using Everywhere.Interop;

namespace Everywhere.Linux.Interop;

public class LinuxNativeHelper : INativeHelper
{
    public bool IsInstalled => false; // implement proper detection if needed

    public bool IsAdministrator => false; // Linux elevation detection omitted

    public bool IsUserStartupEnabled { get; set; }

    public bool IsAdministratorStartupEnabled { get; set; }

    private readonly ILinuxDisplayBackend? _backend = ServiceLocator.Resolve<ILinuxDisplayBackend>();

    public void RestartAsAdministrator()
    {
        // No-op on Linux by default. Could re-exec with sudo if desired.
        ShowDesktopNotification("if Administrator needed on Linux, Please re-exec with sudo.");
    }

    public void SetWindowNoFocus(Window window)
    {
        _backend?.SetWindowNoFocus(window);
    }

    public void SetWindowHitTestInvisible(Window window)
    {
        _backend?.SetWindowHitTestInvisible(window);
    }

    public void SetWindowCornerRadius(Window window, CornerRadius cornerRadius)
    {
        // Currently not implemented for Linux
        _backend?.SetWindowCornerRadius(window, cornerRadius);
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
