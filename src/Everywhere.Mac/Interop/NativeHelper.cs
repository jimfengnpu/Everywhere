using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Everywhere.Interop;

namespace Everywhere.Mac.Interop;

// This implementation leverages the Microsoft.macOS SDK (or Xamarin.Mac)
// for type-safe access to native macOS APIs.
public partial class NativeHelper : INativeHelper
{
    // A unique identifier for your app, used for notifications and launch services.
    private const string AppBundleIdentifier = "com.sylinko.everywhere";
    private static readonly string UserLaunchAgentPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library/LaunchAgents",
        $"{AppBundleIdentifier}.plist");

    // The main application bundle.
    private static readonly NSBundle MainBundle = NSBundle.MainBundle;

    /// <summary>
    /// On macOS, an app is generally considered "installed" if it's running from the /Applications directory.
    /// </summary>
    public bool IsInstalled => MainBundle.BundlePath.StartsWith("/Applications", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the current process is running as root (EUID 0).
    /// Note: GUI applications on macOS should almost never run as root.
    /// Administrative tasks are handled via user-prompted authorization.
    /// </summary>
    public bool IsAdministrator => LibC.geteuid() == 0;

    /// <summary>
    /// Manages whether the app starts automatically on user login.
    /// This uses the modern SMLoginItemSetEnabled API.
    /// Note: This requires a helper XPC service or a separate launcher app bundled inside the main app,
    /// which is the Apple-recommended way. For simplicity, we use the older, less reliable LaunchAgent method here.
    /// A full implementation would be more complex.
    /// </summary>
    public bool IsUserStartupEnabled
    {
        get => File.Exists(UserLaunchAgentPath);
        set
        {
            if (value)
            {
                // Ensure the directory exists.
                Directory.CreateDirectory(Path.GetDirectoryName(UserLaunchAgentPath)!);

                var plist =
                    $"""
                     <?xml version="1.0" encoding="UTF-8"?>
                     <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                     <plist version="1.0">
                     <dict>
                         <key>Label</key>
                         <string>{AppBundleIdentifier}</string>
                         <key>ProgramArguments</key>
                         <array>
                             <string>{MainBundle.ExecutablePath}</string>
                             <string>--autorun</string>
                         </array>
                         <key>RunAtLoad</key>
                         <true/>
                     </dict>
                     </plist>
                     """;
                File.WriteAllText(UserLaunchAgentPath, plist);
            }
            else
            {
                if (File.Exists(UserLaunchAgentPath))
                {
                    File.Delete(UserLaunchAgentPath);
                }
            }
        }
    }

    /// <summary>
    /// System-wide startup (LaunchDaemons) is not typically managed by the app itself on macOS.
    /// This is handled by installers with administrator privileges.
    /// </summary>
    public bool IsAdministratorStartupEnabled { get; set; }

    /// <summary>
    /// Restarts the application by asking the system for administrator privileges.
    /// This will present a standard macOS authentication dialog to the user.
    /// </summary>
    public void RestartAsAdministrator()
    {
        if (IsAdministrator) return;

        var processPath = MainBundle.ExecutablePath;
        if (string.IsNullOrEmpty(processPath))
        {
            throw new InvalidOperationException("Cannot determine the application path.");
        }

        // Use AppleScript to request elevation. This is the standard way.
        var script = $"do shell script \"nohup \'{processPath}\' --ui &\" with administrator privileges";
        var appleScript = new NSAppleScript(script);
        appleScript.ExecuteAndReturnError(out var errorInfo);
        if (errorInfo is not { Count: > 0 })
        {
            // If the script fails (e.g., user cancels), an error is returned.
            // If successful, the new process is launched, and we can exit.
            Environment.Exit(0);
        }
    }

    /// <summary>
    /// Gets a bitmap from the clipboard using the native NSPasteboard API.
    /// </summary>
    public Task<WriteableBitmap?> GetClipboardBitmapAsync()
    {
        var pasteboard = NSPasteboard.GeneralPasteboard;
        // Find the best available image type on the clipboard.
        var imageType = pasteboard.GetAvailableTypeFromArray([NSPasteboard.NSTiffType]);

        if (imageType is not { Length: > 0 })
        {
            return Task.FromResult<WriteableBitmap?>(null);
        }

        // Create an NSImage from the clipboard data.
        var image = new NSImage(pasteboard);
        if (image.Size.Width <= 0 || image.Size.Height <= 0)
        {
            return Task.FromResult<WriteableBitmap?>(null);
        }

        // Convert NSImage to a format Avalonia can use (e.g., TIFF).
        var tiffData = image.AsTiff();
        if (tiffData == null)
        {
            return Task.FromResult<WriteableBitmap?>(null);
        }

        // Load the TIFF data into a WriteableBitmap.
        using var stream = tiffData.AsStream();
        var writeableBitmap = WriteableBitmap.Decode(stream);
        return Task.FromResult<WriteableBitmap?>(writeableBitmap);
    }

    /// <summary>
    /// Shows a desktop notification using the UserNotifications framework.
    /// </summary>
    public void ShowDesktopNotification(string message, string? title = null)
    {
        var notification = new NSUserNotification
        {
            Title = title ?? "Everywhere",
            InformativeText = message,
            SoundName = NSUserNotification.NSUserNotificationDefaultSoundName,
            Identifier = Guid.NewGuid().ToString() // Unique ID
        };

        NSUserNotificationCenter.DefaultUserNotificationCenter.DeliverNotification(notification);
    }

    /// <summary>
    /// Reveals a file in Finder.
    /// </summary>
    public void OpenFileLocation(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath)) return;

        if (Path.GetDirectoryName(fullPath) is not { Length: > 0 } directoryPath)
        {
            // root, just open finder
            NSWorkspace.SharedWorkspace.SelectFile(fullPath, "/");
        }
        else
        {
            NSWorkspace.SharedWorkspace.SelectFile(fullPath, directoryPath);
        }
    }

    // Helper for the IsAdministrator check.
    private static partial class LibC
    {
        [LibraryImport("libc")]
        internal static partial uint geteuid();
    }
}