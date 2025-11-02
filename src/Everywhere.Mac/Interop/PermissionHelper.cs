namespace Everywhere.Mac.Interop;

/// <summary>
/// Helper class for managing macOS Accessibility permissions required for global event listening.
/// </summary>
public static class PermissionHelper
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

    /// <summary>
    /// Requests Screen Recording access by showing the system prompt.
    /// This will open System Settings and guide the user.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public static void RequestScreenRecordingAccess()
    {
        // There is no official API to request Screen Recording access.
        // A common workaround is to attempt to capture the screen, which will trigger the prompt.
        // However, this requires additional implementation and is not included here.
        throw new NotImplementedException("Screen Recording access request is not implemented.");
    }

    // ReSharper disable once InconsistentNaming
    private static bool AXIsProcessTrustedWithOptions(NSDictionary options)
    {
        return AXIsProcessTrustedWithOptions(options.Handle);
    }

    // C# binding for the C function AXIsProcessTrustedWithOptions.
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool AXIsProcessTrustedWithOptions(nint options);
}