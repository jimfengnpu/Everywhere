using Everywhere.Common;
using Everywhere.Mac.Interop;

namespace Everywhere.Mac.Initialization;

/// <summary>
/// Asks for necessary permissions on macOS during application initialization.
/// Including:
/// - Accessibility permissions for global event listening.
/// - Screen Recording permissions for capturing screen content.
/// </summary>
public class PermissionInitializer : IAsyncInitializer
{
    public AsyncInitializerPriority Priority => AsyncInitializerPriority.Highest;

    public Task InitializeAsync()
    {
        PermissionHelper.RequestAccessibilityAccess();
        // PermissionHelper.RequestScreenRecordingAccess();
        return Task.CompletedTask;
    }
}