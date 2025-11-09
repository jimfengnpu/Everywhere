using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Everywhere.Views.Configuration;

public class DebugFeaturesControl : ContentControl
{
    public DebugFeaturesControl(ILauncher launcher, ToastManager toastManager, ILogger<DebugFeaturesControl> logger)
    {
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new Button
                {
                    [!ContentProperty] = new DynamicResourceKey(LocaleKey.Settings_Common_EditSettingsFile_Button_Content).ToBinding(),
                    [ButtonAssist.IconSizeProperty] = 18,
                    [ButtonAssist.IconProperty] = new LucideIcon
                    {
                        Kind = LucideIconKind.FileJson2
                    },
                    Classes = { "Outline" },
                    Command = new AsyncRelayCommand(async () =>
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

                            var launched = await launcher.LaunchUriAsync(settingsUri);
                            if (!launched)
                            {
                                throw new InvalidOperationException($"Unable to launch: {settingsUri}");
                            }
                        }
                        catch (Exception ex)
                        {
                            ex = HandledSystemException.Handle(ex);
                            logger.LogError(ex, "Failed to open settings file.");
                            toastManager
                                .CreateToast(LocaleKey.Common_Error.I18N())
                                .WithContent(ex.GetFriendlyMessage())
                                .DismissOnClick()
                                .OnBottomRight()
                                .ShowError();
                        }
                    })
                },
                new Button
                {
                    [!ContentProperty] = new DynamicResourceKey(LocaleKey.Settings_Common_OpenLogsFolder_Button_Content).ToBinding(),
                    [ButtonAssist.IconSizeProperty] = 18,
                    [ButtonAssist.IconProperty] = new LucideIcon
                    {
                        Kind = LucideIconKind.FolderOpen
                    },
                    Classes = { "Outline" },
                    Command = new AsyncRelayCommand(async () =>
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

                            var launched = await launcher.LaunchUriAsync(logsUri);
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
                    })
                }
            }
        };
    }
}
