using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Configuration;

namespace Everywhere.Chat.Plugins;

[GeneratedSettingsItems]
public partial class WebSearchEngineProvider : ObservableObject
{
    [HiddenSettingsItem]
    [IgnoreDataMember]
    public required string Id { get; init; } = string.Empty;

    [JsonIgnore]
    [HiddenSettingsItem]
    public string DisplayName { get; init; } = string.Empty;

    [DynamicResourceKey(
        LocaleKey.WebSearchEngineProvider_EndPoint_Header,
        LocaleKey.WebSearchEngineProvider_EndPoint_Description)]
    public required Customizable<string> EndPoint { get; init; }

    [IgnoreDataMember]
    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.WebSearchEngineProvider_ApiKey_Header,
        LocaleKey.WebSearchEngineProvider_ApiKey_Description)]
    [SettingsItem(IsVisibleBindingPath = nameof(IsApiKeyRequired))]
    [SettingsStringItem(IsPassword = true)]
    public partial string? ApiKey { get; set; }

    [JsonIgnore]
    [HiddenSettingsItem]
    public bool IsSearchEngineIdVisible => Id.Equals("google", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    [HiddenSettingsItem]
    public bool IsApiKeyRequired => !Id.Equals("searxng", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// for Google search engine, this is the search engine ID.
    /// </summary>
    [IgnoreDataMember]
    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.WebSearchEngineProvider_SearchEngineId_Header,
        LocaleKey.WebSearchEngineProvider_SearchEngineId_Description)]
    [SettingsItem(IsVisibleBindingPath = nameof(IsSearchEngineIdVisible))]
    public partial string? SearchEngineId { get; set; }

    public override bool Equals(object? obj) => obj is WebSearchEngineProvider provider && Id == provider.Id;

    public override int GetHashCode() => Id.GetHashCode();
}