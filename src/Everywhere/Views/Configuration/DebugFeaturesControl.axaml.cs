using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Everywhere.Views.Configuration;

public partial class DebugFeaturesControl(ToastManager toastManager, ILogger<DebugFeaturesControl> logger)
    : TemplatedControl
{
    [RelayCommand]
    private static async Task EditSettingsFileAsync()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Everywhere",
                "settings.json");
            if (!Uri.TryCreate(settingsPath, UriKind.Absolute, out var settingsUri))
            {
                throw new InvalidOperationException($"Invalid settings path: {settingsPath}");
            }

            var launched = await ServiceLocator.Resolve<ILauncher>().LaunchUriAsync(settingsUri);
            if (!launched)
            {
                throw new InvalidOperationException($"Unable to launch: {settingsUri}");
            }
        }
        catch (Exception ex)
        {
            ex = HandledSystemException.Handle(ex);
            ServiceLocator.Resolve<ILogger<DebugFeaturesControl>>().LogError(ex, "Failed to open settings file.");
            ServiceLocator.Resolve<ToastManager>()
                .CreateToast(LocaleKey.Common_Error.I18N())
                .WithContent(ex.GetFriendlyMessage())
                .DismissOnClick()
                .OnBottomRight()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task OpenLogsFolderAsync()
    {
        try
        {
            var logsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Everywhere",
                "logs");
            if (!Directory.Exists(logsPath))
            {
                Directory.CreateDirectory(logsPath);
            }

            if (!Uri.TryCreate(logsPath, UriKind.Absolute, out var logsUri))
            {
                throw new InvalidOperationException($"Invalid logs path: {logsPath}");
            }

            var launched = await ServiceLocator.Resolve<ILauncher>().LaunchUriAsync(logsUri);
            if (!launched)
            {
                throw new InvalidOperationException($"Unable to launch: {logsUri}");
            }
        }
        catch (Exception ex)
        {
            ex = HandledSystemException.Handle(ex);
            logger.LogError(ex, "Failed to open logs folder.");
            toastManager
                .CreateToast(LocaleKey.Common_Error.I18N())
                .WithContent(ex.GetFriendlyMessage())
                .DismissOnClick()
                .OnBottomRight()
                .ShowError();
        }
    }
}
