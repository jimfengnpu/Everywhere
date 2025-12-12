using Everywhere.Common;
using Everywhere.Mac.Interop;

namespace Everywhere.Mac.Initialization;

/// <summary>
/// Asks for necessary permissions on macOS during application initialization.
/// Including:
/// - Accessibility permissions for global event listening.
/// </summary>
public class PermissionInitializer : IAsyncInitializer
{
    public AsyncInitializerPriority Priority => AsyncInitializerPriority.Highest;

    public Task InitializeAsync()
    {
        PermissionHelper.RequestAccessibilityAccess();
        return Task.CompletedTask;
    }
}