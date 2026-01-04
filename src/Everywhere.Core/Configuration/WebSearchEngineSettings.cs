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
    public partial ObservableCollection<WebSearchEngineProvider> WebSearchEngineProviders { get; set; }

    [HiddenSettingsItem]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedWebSearchEngineProvider))]
    public partial WebSearchEngineProviderId SelectedWebSearchEngineProviderId { get; set; }

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
            SelectedWebSearchEngineProviderId = value?.Id ?? default;
        }
    }

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial ObservableCollection<ApiKey> ApiKeys { get; set; }

    public WebSearchEngineSettings()
    {
        ApiKeys = [];
        WebSearchEngineProviders = [
            new WebSearchEngineProvider(ApiKeys)
            {
                Id = WebSearchEngineProviderId.Google,
                DisplayName = "Google",
                EndPoint = new Customizable<string>("https://customsearch.googleapis.com", isDefaultValueReadonly: true)
            },
            new WebSearchEngineProvider(ApiKeys)
            {
                Id = WebSearchEngineProviderId.Tavily,
                DisplayName = "Tavily",
                EndPoint = new Customizable<string>("https://api.tavily.com", isDefaultValueReadonly: true)
            },
            new WebSearchEngineProvider(ApiKeys)
            {
                Id = WebSearchEngineProviderId.Brave,
                DisplayName = "Brave",
                EndPoint = new Customizable<string>("https://api.search.brave.com/res/v1/web/search", isDefaultValueReadonly: true)
            },
            new WebSearchEngineProvider(ApiKeys)
            {
                Id = WebSearchEngineProviderId.Bocha,
                DisplayName = "Bocha",
                EndPoint = new Customizable<string>("https://api.bochaai.com/v1/web-search", isDefaultValueReadonly: true)
            },
            new WebSearchEngineProvider(ApiKeys)
            {
                Id = WebSearchEngineProviderId.Jina,
                DisplayName = "Jina",
                EndPoint = new Customizable<string>("https://s.jina.ai", isDefaultValueReadonly: true)
            },
            new WebSearchEngineProvider(ApiKeys)
            {
                Id = WebSearchEngineProviderId.SearXNG,
                DisplayName = "SearXNG",
                EndPoint = new Customizable<string>("https://searxng.example.com/search", isDefaultValueReadonly: true)
            },
        ];
    }
}