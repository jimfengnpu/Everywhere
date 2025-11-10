using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Chat.Plugins;

namespace Everywhere.Configuration;

[GeneratedSettingsItems]
public partial class WebSearchEngineSettings : ObservableObject
{
    [HiddenSettingsItem]
    [ObservableProperty]
    public partial ObservableCollection<WebSearchEngineProvider> WebSearchEngineProviders { get; set; } = [];

    [HiddenSettingsItem]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedWebSearchEngineProvider))]
    public partial string? SelectedWebSearchEngineProviderId { get; set; }

    [JsonIgnore]
    [DynamicResourceKey(
        LocaleKey.WebSearchEngineProvider_Header,
        LocaleKey.WebSearchEngineProvider_Description)]
    [SettingsItems(IsExpanded = true)]
    [SettingsSelectionItem(nameof(WebSearchEngineProviders), DataTemplateKey = typeof(WebSearchEngineProvider))]
    public WebSearchEngineProvider? SelectedWebSearchEngineProvider
    {
        get => WebSearchEngineProviders.FirstOrDefault(p => p.Id == SelectedWebSearchEngineProviderId);
        set
        {
            if (Equals(SelectedWebSearchEngineProviderId, value?.Id)) return;
            SelectedWebSearchEngineProviderId = value?.Id;
        }
    }
}