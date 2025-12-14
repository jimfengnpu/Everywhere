using System.Runtime.InteropServices;

namespace Everywhere.Mac.Interop;

/// <summary>
/// Helper class for managing macOS Accessibility permissions required for global event listening.
/// </summary>
public static partial class PermissionHelper
{
    // Key for the options dictionary.
    private static readonly NSString AxTrustedCheckOptionPrompt = new("AXTrustedCheckOptionPrompt");

    /// <summary>
    /// Checks if the application has been granted Accessibility access.
    /// </summary>
    public static bool IsAccessibilityTrusted()
    {
        // For sandboxed apps, this will always be false.
        // For non-sandboxed apps, it checks the system settings.
        return AXIsProcessTrustedWithOptions(new NSDictionary(AxTrustedCheckOptionPrompt, NSNumber.FromBoolean(false)));
    }

    /// <summary>
    /// Requests Accessibility access by showing the system prompt.
    /// This will open System Settings and guide the user.
    /// </summary>
    public static void RequestAccessibilityAccess()
    {
        AXIsProcessTrustedWithOptions(new NSDictionary(AxTrustedCheckOptionPrompt, NSNumber.FromBoolean(true)));
    }

    // ReSharper disable once InconsistentNaming
    private static bool AXIsProcessTrustedWithOptions(NSDictionary options)
    {
        return AXIsProcessTrustedWithOptions(options.Handle);
    }

    // C# binding for the C function AXIsProcessTrustedWithOptions.
    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AXIsProcessTrustedWithOptions(nint options);
}