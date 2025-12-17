using System.Text.Json;
using Everywhere.Common;
using Everywhere.Initialization;
using Everywhere.Views.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WritableJsonConfiguration;

namespace Everywhere.Configuration;

public static class SettingsExtensions
{
    public static IServiceCollection AddSettings(this IServiceCollection services) => services
        .AddKeyedSingleton<IConfiguration>(
            typeof(Settings),
            (xx, _) =>
            {
                IConfiguration configuration;
                var settingsJsonPath = Path.Combine(
                    xx.GetRequiredService<IRuntimeConstantProvider>().Get<string>(RuntimeConstantType.WritableDataPath),
                    "settings.json");
                var loggerFactory = xx.GetRequiredService<ILoggerFactory>();
                try
                {
                    configuration = WritableJsonConfigurationFabric.Create(settingsJsonPath, loggerFactory: loggerFactory);
                }
                catch (Exception ex) when (ex is JsonException or InvalidDataException)
                {
                    File.Delete(settingsJsonPath);
                    configuration = WritableJsonConfigurationFabric.Create(settingsJsonPath, loggerFactory: loggerFactory);
                }
                return configuration;
            })
        .AddSingleton<Settings>(xx =>
        {
            var configuration = xx.GetRequiredKeyedService<IConfiguration>(typeof(Settings));
            var settings = new Settings();
            configuration.Bind(settings);
            return settings;
        })
        .AddTransient<SoftwareUpdateControl>()
        .AddTransient<RestartAsAdministratorControl>()
        .AddTransient<DebugFeaturesControl>()
        .AddSingleton<KeyValueStorage>()
        .AddSingleton<IKeyValueStorage>(sp => sp.GetRequiredService<KeyValueStorage>())
        .AddTransient<IAsyncInitializer>(sp => sp.GetRequiredService<KeyValueStorage>())
        .AddTransient<IAsyncInitializer, SettingsInitializer>();
}