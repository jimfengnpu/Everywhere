using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Everywhere.AI;
using Everywhere.Chat;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Extensions;
using Everywhere.Initialization;
using Everywhere.Interop;
using Everywhere.ViewModels;
using Everywhere.Views;
using Everywhere.Views.Pages;
using Everywhere.Linux.Configuration;
using Everywhere.Linux.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SoftwareUpdater = Everywhere.Linux.Common.SoftwareUpdater;

namespace Everywhere.Linux;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Entrance.Initialize(args);

        ServiceLocator.Build(x => x

                #region Basic

                .AddLogging(builder => builder
                    .AddSerilog(dispose: true)
                    .AddFilter<SerilogLoggerProvider>("Microsoft.EntityFrameworkCore", LogLevel.Debug))
                .AddSingleton<IRuntimeConstantProvider, RuntimeConstantProvider>()
                .AddSingleton<ILinuxDisplayBackend, LinuxDisplayBackend>()
                .AddSingleton<AtspiService>()
                .AddSingleton<IVisualElementContext, LinuxVisualElementContext>()
                .AddSingleton<IHotkeyListener, LinuxHotkeyListener>()
                .AddSingleton<INativeHelper, LinuxNativeHelper>()
                .AddSingleton<ISoftwareUpdater, SoftwareUpdater>()
                .AddSettings()
                .AddWatchdogManager()

                #endregion

                #region Avalonia Basic

                .AddDialogManagerAndToastManager()
                .AddTransient<IClipboard>(_ =>
                    Application.Current.As<App>()?.TopLevel.Clipboard ??
                    throw new InvalidOperationException("Clipboard is not available."))
                .AddTransient<IStorageProvider>(_ =>
                    Application.Current.As<App>()?.TopLevel.StorageProvider ??
                    throw new InvalidOperationException("StorageProvider is not available."))
                .AddTransient<ILauncher>(_ =>
                    Application.Current.As<App>()?.TopLevel.Launcher ??
                    throw new InvalidOperationException("Launcher is not available."))

                #endregion

                #region View & ViewModel

                .AddSingleton<VisualTreeDebugger>()
                .AddSingleton<ChatWindowViewModel>()
                .AddSingleton<ChatWindow>()
                .AddTransient<IMainViewPageFactory, SettingsCategoryPageFactory>()
                .AddSingleton<ChatPluginPageViewModel>()
                .AddSingleton<IMainViewPage, ChatPluginPage>()
                .AddSingleton<AboutPageViewModel>()
                .AddSingleton<IMainViewPage, AboutPage>()
                .AddTransient<WelcomeViewModel>()
                .AddTransient<WelcomeView>()
                .AddTransient<ChangeLogViewModel>()
                .AddTransient<ChangeLogView>()
                .AddSingleton<MainViewModel>()
                .AddSingleton<MainView>()

                #endregion

                #region Database

                .AddDatabaseAndStorage()

                #endregion

                #region Chat Plugins

                .AddTransient<BuiltInChatPlugin, VisualTreePlugin>()
                .AddTransient<BuiltInChatPlugin, WebSearchEnginePlugin>()
                .AddTransient<BuiltInChatPlugin, FileSystemPlugin>()
                // PowerShell plugin is Windows-specific; do not register it on Linux.

                #endregion

                #region Chat

                .AddSingleton<IKernelMixinFactory, KernelMixinFactory>()
                .AddSingleton<IChatPluginManager, ChatPluginManager>()
                .AddSingleton<IChatService, ChatService>()
                .AddChatContextManager()

                #endregion

                #region Initialize

                .AddTransient<IAsyncInitializer, HotkeyInitializer>()
                .AddTransient<IAsyncInitializer, UpdaterInitializer>()

            #endregion

        );

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}