using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Chat.Plugins;
using Everywhere.Views;
using ShadUI;

namespace Everywhere.ViewModels;

public partial class ChatPluginPageViewModel(IChatPluginManager manager) : BusyViewModelBase
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
                LocaleResolver.Common_OK,
                (_, e) => e.Cancel = !form.Configuration.Validate())
            .WithCancelButton(LocaleResolver.Common_Cancel)
            .ShowAsync();
        if (result != DialogResult.Primary) return;
        if (form.Configuration.HasErrors) return;

        try
        {
            SelectedPlugin = manager.CreateMcpPlugin(form.Configuration);
        }
        catch (Exception e)
        {
            ToastExceptionHandler.HandleException(e, "Failed to add MCP Plugin");
        }
    }

    [RelayCommand]
    private Task StartMcpPluginAsync(McpChatPlugin plugin, CancellationToken cancellationToken) => ExecuteBusyTaskAsync(
        token => Task.Run(() => manager.StartMcpClientAsync(plugin, token), token),
        ToastExceptionHandler,
        cancellationToken: cancellationToken);

    [RelayCommand]
    private Task StopMcpPluginAsync(McpChatPlugin plugin, CancellationToken cancellationToken) => ExecuteBusyTaskAsync(
        token => Task.Run(() => manager.StopMcpClientAsync(plugin), token),
        ToastExceptionHandler,
        cancellationToken: cancellationToken);

    [RelayCommand]
    private Task EditMcpPluginAsync(McpChatPlugin plugin, CancellationToken cancellationToken) => ExecuteBusyTaskAsync(
        async token =>
        {
            var form = new McpTransportConfigurationForm
            {
                Configuration = JsonSerializer.Deserialize(
                    JsonSerializer.Serialize(
                        plugin.TransportConfiguration,
                        McpTransportConfigurationJsonSerializerContext.Default.McpTransportConfiguration),
                    McpTransportConfigurationJsonSerializerContext.Default.McpTransportConfiguration) ?? new StdioMcpTransportConfiguration()
            };
            var result = await DialogManager
                .CreateDialog(form, "Edit MCP Plugin")
                .WithPrimaryButton(
                    LocaleResolver.Common_OK,
                    (_, e) => e.Cancel = !form.Configuration.Validate())
                .WithCancelButton(LocaleResolver.Common_Cancel)
                .ShowAsync(token);
            if (result != DialogResult.Primary) return;
            if (form.Configuration.HasErrors) return;

            await Task.Run(() => manager.UpdateMcpPluginAsync(plugin, form.Configuration), token);
        },
        ToastExceptionHandler,
        cancellationToken: cancellationToken);

    [RelayCommand]
    private Task RemoveMcpPluginAsync(McpChatPlugin plugin, CancellationToken cancellationToken) => ExecuteBusyTaskAsync(
        async token =>
        {
            var result = await DialogManager.CreateDialog("Are you sure you want to delete this MCP plugin?")
                .WithPrimaryButton(LocaleResolver.Common_Yes)
                .WithCancelButton(LocaleResolver.Common_No)
                .ShowAsync(token);
            if (result != DialogResult.Primary) return;

            await Task.Run(() => manager.RemoveMcpPluginAsync(plugin), token);

            if (SelectedPlugin == plugin) SelectedPlugin = null;
        },
        ToastExceptionHandler,
        cancellationToken: cancellationToken);
}