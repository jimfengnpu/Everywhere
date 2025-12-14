using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Everywhere.Configuration;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Everywhere.Views.Configuration;

public partial class SoftwareUpdateControl(
    Settings settings,
    ISoftwareUpdater softwareUpdater,
    ToastManager toastManager,
    ILogger<SoftwareUpdateControl> logger
)
    : TemplatedControl
{
    public Settings Settings { get; } = settings;

    public ISoftwareUpdater SoftwareUpdater { get; } = softwareUpdater;

    public string CurrentVersion => SoftwareUpdater.CurrentVersion.ToString(3);

    [RelayCommand]
    private static async Task ShowReleaseNotesAsync()
    {
        await ServiceLocator.Resolve<ILauncher>()
            .LaunchUriAsync(
                new Uri(
                    "https://github.com/DearVa/Everywhere/releases",
                    UriKind.Absolute));
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            await SoftwareUpdater.CheckForUpdatesAsync();

            var toastMessage = SoftwareUpdater.LatestVersion is null ?
                new DynamicResourceKey(LocaleKey.CommonSettings_SoftwareUpdate_Toast_AlreadyLatestVersion) :
                new FormattedDynamicResourceKey(
                    LocaleKey.CommonSettings_SoftwareUpdate_Toast_NewVersionFound,
                    new DirectResourceKey(SoftwareUpdater.LatestVersion));
            toastManager
                .CreateToast(LocaleResolver.Common_Info)
                .WithContent(toastMessage)
                .DismissOnClick()
                .OnBottomRight()
                .ShowInfo();
        }
        catch (Exception ex)
        {
            ex = new HandledException(ex, new DynamicResourceKey(LocaleKey.CommonSettings_SoftwareUpdate_Toast_CheckForUpdatesFailed_Content));
            logger.LogError(ex, "Failed to check for updates.");
            ShowErrorToast(ex);
        }
    }

    public static readonly DirectProperty<SoftwareUpdateControl, IAsyncRelayCommand> PerformUpdateCommandProperty =
        AvaloniaProperty.RegisterDirect<SoftwareUpdateControl, IAsyncRelayCommand>(
            nameof(PerformUpdateCommand),
            o => o.PerformUpdateCommand);

    public IAsyncRelayCommand PerformUpdateCommand => new AsyncRelayCommand(async () =>
    {
        try
        {
            var progress = new Progress<double>();
            var cts = new CancellationTokenSource();
            toastManager
                .CreateToast(LocaleResolver.Common_Info)
                .WithContent(LocaleResolver.CommonSettings_SoftwareUpdate_Toast_DownloadingUpdate)
                .WithProgress(progress)
                .WithCancellationTokenSource(cts)
                .WithDelay(0d)
                .OnBottomRight()
                .ShowInfo();
            await SoftwareUpdater.PerformUpdateAsync(progress, cts.Token);
        }
        catch (Exception ex)
        {
            ex = new HandledException(ex, new DynamicResourceKey(LocaleKey.CommonSettings_SoftwareUpdate_Toast_UpdateFailed_Content));
            logger.LogError(ex, "Failed to perform update.");
            ShowErrorToast(ex);
        }
    });

    private void ShowErrorToast(Exception ex) => toastManager
        .CreateToast(LocaleResolver.Common_Error)
        .WithContent(ex.GetFriendlyMessage())
        .DismissOnClick()
        .OnBottomRight()
        .ShowError();
}