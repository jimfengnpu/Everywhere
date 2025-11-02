using CommunityToolkit.Mvvm.ComponentModel;

namespace Everywhere.Configuration;

public partial class ProxySettings : SettingsCategory
{
    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    [SettingsItem(IsVisibleBindingPath = nameof(IsEnabled))]
    [SettingsStringItem(Watermark = "http://127.0.0.1:7890")]
    public partial Customizable<string> Endpoint { get; set; } = "http://127.0.0.1:7890";

    [ObservableProperty]
    [SettingsItem(IsVisibleBindingPath = nameof(IsEnabled))]
    public partial bool BypassOnLocal { get; set; } = true;

    [ObservableProperty]
    [SettingsItem(IsVisibleBindingPath = nameof(IsEnabled))]
    [SettingsStringItem(Watermark = "www.example.com", IsMultiline = true, Height = 96)]
    public partial string? BypassList { get; set; }

    [ObservableProperty]
    [SettingsItem(IsVisibleBindingPath = nameof(IsEnabled))]
    public partial bool UseAuthentication { get; set; }

    [ObservableProperty]
    [SettingsItem(IsVisibleBindingPath = $"{nameof(IsEnabled)} && {nameof(UseAuthentication)}")]
    [SettingsStringItem(Watermark = "Username")]
    public partial string? Username { get; set; }

    [ObservableProperty]
    [SettingsItem(IsVisibleBindingPath = $"{nameof(IsEnabled)} && {nameof(UseAuthentication)}")]
    [SettingsStringItem(IsPassword = true)]
    public partial string? Password { get; set; }
}