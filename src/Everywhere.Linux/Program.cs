using Avalonia;
using Avalonia.Controls;
using Everywhere.AI;
using Everywhere.Chat;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Extensions;
using Everywhere.Initialization;
using Everywhere.Interop;
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
                .AddSingleton<LinuxDisplayBackend>()
                .AddSingleton<ILinuxDisplayBackend>(sp => sp.GetRequiredService<LinuxDisplayBackend>())
                .AddSingleton<IWindowHelper>(sp => sp.GetRequiredService<LinuxDisplayBackend>())
                .AddSingleton<IVisualElementContext, LinuxVisualElementContext>()
                .AddSingleton<IShortcutListener, LinuxShortcutListener>()
                .AddSingleton<INativeHelper, LinuxNativeHelper>()
                .AddSingleton<ISoftwareUpdater, SoftwareUpdater>()
                .AddSettings()
                .AddWatchdogManager()
                .ConfigureNetwork()
                .AddAvaloniaBasicServices()
                .AddViewsAndViewModels()
                .AddDatabaseAndStorage()

                #endregion

                #region Chat Plugins

                .AddTransient<BuiltInChatPlugin, VisualTreePlugin>()
                .AddTransient<BuiltInChatPlugin, WebBrowserPlugin>()
                .AddTransient<BuiltInChatPlugin, FileSystemPlugin>()

                #endregion

                #region Chat

                .AddSingleton<IKernelMixinFactory, KernelMixinFactory>()
                .AddSingleton<IChatPluginManager, ChatPluginManager>()
                .AddSingleton<IChatService, ChatService>()
                .AddChatContextManager()

                #endregion

                #region Initialize

                .AddTransient<IAsyncInitializer, ChatWindowInitializer>()
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