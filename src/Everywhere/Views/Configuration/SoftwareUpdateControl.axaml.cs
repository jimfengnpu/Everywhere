using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Everywhere.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
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
    public Settings Settings { get; set; } = settings;
    public ISoftwareUpdater SoftwareUpdater { get; set; } = softwareUpdater;
    public ToastManager ToastManager { get; set; } = toastManager;
    public ILogger<SoftwareUpdateControl> Logger { get; set; } = logger;


    public static readonly DirectProperty<SoftwareUpdateControl, FormattedDynamicResourceKey> CurrentVersionProperty =
        AvaloniaProperty.RegisterDirect<SoftwareUpdateControl, FormattedDynamicResourceKey>(
            nameof(CurrentVersion),
            o => o.CurrentVersion);

    public FormattedDynamicResourceKey CurrentVersion => new(
        LocaleKey.Settings_Common_SoftwareUpdate_TextBlock_Run1_Text,
        new DirectResourceKey(SoftwareUpdater.CurrentVersion.ToString(3)));

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
                new DynamicResourceKey(LocaleKey.Settings_Common_SoftwareUpdate_Toast_AlreadyLatestVersion) :
                new FormattedDynamicResourceKey(
                    LocaleKey.Settings_Common_SoftwareUpdate_Toast_NewVersionFound,
                    new DirectResourceKey(SoftwareUpdater.LatestVersion));
            ToastManager
                .CreateToast(LocaleKey.Common_Info.I18N())
                .WithContent(toastMessage)
                .DismissOnClick()
                .OnBottomRight()
                .ShowInfo();
        }
        catch (Exception ex)
        {
            ex = new HandledException(ex, LocaleKey.Settings_Common_SoftwareUpdate_Toast_CheckForUpdatesFailed_Content);
            Logger.LogError(ex, "Failed to check for updates.");
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
            ToastManager
                .CreateToast(LocaleKey.Common_Info.I18N())
                .WithContent(LocaleKey.Settings_Common_SoftwareUpdate_Toast_DownloadingUpdate.I18N())
                .WithProgress(progress)
                .WithCancellationTokenSource(cts)
                .WithDelay(0d)
                .OnBottomRight()
                .ShowInfo();
            await SoftwareUpdater.PerformUpdateAsync(progress, cts.Token);
        }
        catch (Exception ex)
        {
            ex = new HandledException(ex, LocaleKey.Settings_Common_SoftwareUpdate_Toast_UpdateFailed_Content);
            Logger.LogError(ex, "Failed to perform update.");
            ShowErrorToast(ex);
        }
    });

    private void ShowErrorToast(Exception ex) => ToastManager
        .CreateToast(LocaleKey.Common_Error.I18N())
        .WithContent(ex.GetFriendlyMessage())
        .DismissOnClick()
        .OnBottomRight()
        .ShowError();
}