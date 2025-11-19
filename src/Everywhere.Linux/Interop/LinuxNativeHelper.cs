using System.Diagnostics;
using Avalonia.Media.Imaging;
using Everywhere.Extensions;
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
        ShowDesktopNotification("if Administrator needed on Linux, Please re-exec with sudo.");
    }

    public Task<WriteableBitmap?> GetClipboardBitmapAsync()
    {
        return Task.FromResult<WriteableBitmap?>(null);
    }

    public void ShowDesktopNotification(string message, string? title = null)
    {
        // Try to use libnotify via command line as a best-effort notification
        var args = $"-u normal \"{title ?? "Everywhere"}\" \"{message}\"";
        Process.Start("notify-send", args);
    }

    public void OpenFileLocation(string fullPath)
    {
        if (fullPath.IsNullOrWhiteSpace()) return;
        var args = $"\"{fullPath}\"";
        Process.Start(new ProcessStartInfo("xdg-open", args) { UseShellExecute = true });
    }
}
