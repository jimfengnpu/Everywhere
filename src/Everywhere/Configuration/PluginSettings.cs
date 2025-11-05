using System.Collections.ObjectModel;
using Everywhere.Chat.Permissions;
using Everywhere.Chat.Plugins;
using ObservableCollections;

namespace Everywhere.Configuration;

public class PluginSettings : SettingsCategory
{
    public override string Header => "Plugin";

    /// <summary>
    /// Gets or sets whether each plugin is enabled.
    /// </summary>
    /// <remarks>
    /// The key is in the format of "PluginKey.FunctionName".
    /// Plugins are disabled if the key is not present.
    /// But Functions are enabled by default if the plugin is enabled.
    /// </remarks>
    [HiddenSettingsItem]
    public ObservableDictionary<string, bool> IsEnabled { get; set; } = new();

    /// <summary>
    /// Gets or sets the granted permissions for each plugin function.
    /// The key is in the format of "PluginKey.FunctionName.[Id]".
    /// </summary>
    [HiddenSettingsItem]
    public ObservableDictionary<string, ChatFunctionPermissions> GrantedPermissions { get; set; } = new();

    [HiddenSettingsItem]
    public ObservableCollection<McpChatPlugin> McpPlugins { get; set; } = [];

    [HiddenSettingsItem]
    public WebSearchEngineSettings WebSearchEngine { get; set; } = new();

    public PluginSettings()
    {
        IsEnabled.CollectionChanged += delegate { OnPropertyChanged(nameof(IsEnabled)); };
        GrantedPermissions.CollectionChanged += delegate { OnPropertyChanged(nameof(GrantedPermissions)); };
    }
}