using Avalonia;
using Avalonia.Media.Imaging;
using Everywhere.Interop;

namespace Everywhere.Darwin.Mock;

public class MockNativeHelper : INativeHelper
{
    public bool IsInstalled { get; set; }
    public bool IsAdministrator { get; set; }
    public bool IsUserStartupEnabled { get; set; }
    public bool IsAdministratorStartupEnabled { get; set; }
    public void RestartAsAdministrator()
    {
    }

    public Task<WriteableBitmap?> GetClipboardBitmapAsync()
    {
        throw new NotImplementedException();
    }

    public void ShowDesktopNotification(string message, string? title = null)
    {
    }

    public void OpenFileLocation(string fullPath)
    {
    }
}