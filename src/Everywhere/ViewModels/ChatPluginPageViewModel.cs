using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Chat.Plugins;
using Everywhere.Views;
using ShadUI;

namespace Everywhere.ViewModels;

public partial class ChatPluginPageViewModel(IChatPluginManager manager) : ReactiveViewModelBase
{
    public IChatPluginManager Manager => manager;

    public ChatPlugin? SelectedPlugin
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(SelectedMcpPlugin));

            // TabItem0 is invisible when there is no SettingsItems, so switch to TabItem1
            if (value is not { SettingsItems.Count: > 0 })
            {
                PluginDetailsTabSelectedIndex = 1;
            }
        }
    }

    public McpChatPlugin? SelectedMcpPlugin => SelectedPlugin as McpChatPlugin;

    [ObservableProperty]
    public partial int PluginDetailsTabSelectedIndex { get; set; }

    [RelayCommand]
    private async Task AddMcpPlugin()
    {
        var form = new McpTransportConfigurationForm();
        var result = await DialogManager
            .CreateDialog(form, "Add MCP Plugin")
            .WithPrimaryButton(
                new DynamicResourceKey(LocaleKey.Common_OK).ToTextBlock(),
                (_, e) => e.Cancel = !form.Configuration.Validate())
            .WithCancelButton(new DynamicResourceKey(LocaleKey.Common_Cancel).ToTextBlock())
            .ShowAsync();
        if (result != DialogResult.Primary) return;
        if (form.Configuration.HasErrors) return;

        try
        {
            SelectedPlugin = manager.AddMcpPlugin(form.Configuration);
        }
        catch (Exception e)
        {
            ToastExceptionHandler.HandleException(e, "Failed to add MCP Plugin");
        }
    }
}