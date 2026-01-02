using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Chat;
using Everywhere.Interop;
using Lucide.Avalonia;

namespace Everywhere.Configuration;

[GeneratedSettingsItems]
public partial class ChatWindowSettings : ObservableObject, ISettingsCategory
{
    [HiddenSettingsItem]
    public DynamicResourceKeyBase DisplayNameKey => new DynamicResourceKey(LocaleKey.SettingsCategory_ChatWindow_Header);

    [HiddenSettingsItem]
    public LucideIconKind Icon => LucideIconKind.MessageCircle;

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.ChatWindowSettings_Shortcut_Header,
        LocaleKey.ChatWindowSettings_Shortcut_Description)]
    [SettingsTemplatedItem]
    public partial KeyboardShortcut Shortcut { get; set; } = new(Key.E, KeyModifiers.Control | KeyModifiers.Shift);

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.ChatWindowSettings_WindowPinMode_Header,
        LocaleKey.ChatWindowSettings_WindowPinMode_Description)]
    public partial ChatWindowPinMode WindowPinMode { get; set; }

    /// <summary>
    /// Temporary chat mode when creating a new chat.
    /// </summary>
    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.ChatWindowSettings_TemporaryChatMode_Header,
        LocaleKey.ChatWindowSettings_TemporaryChatMode_Description)]
    public partial TemporaryChatMode TemporaryChatMode { get; set; }

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.ChatWindowSettings_VisualTreeDetailLevel_Header,
        LocaleKey.ChatWindowSettings_VisualTreeDetailLevel_Description)]
    public partial VisualTreeDetailLevel VisualTreeDetailLevel { get; set; } = VisualTreeDetailLevel.Compact;

    /// <summary>
    /// When enabled, automatically add focused element as attachment when opening chat window.
    /// </summary>
    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.ChatWindowSettings_AutomaticallyAddElement_Header,
        LocaleKey.ChatWindowSettings_AutomaticallyAddElement_Description)]
    public partial bool AutomaticallyAddElement { get; set; } = true;

    /// <summary>
    /// When enabled, chat window can generate response in the background when closed.
    /// </summary>
    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.ChatWindowSettings_AllowRunInBackground_Header,
        LocaleKey.ChatWindowSettings_AllowRunInBackground_Description)]
    public partial bool AllowRunInBackground { get; set; } = true;

    /// <summary>
    /// When enabled, show chat statistics in the chat window.
    /// </summary>
    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.ChatWindowSettings_ShowChatStatistics_Header,
        LocaleKey.ChatWindowSettings_ShowChatStatistics_Description)]
    public partial bool ShowChatStatistics { get; set; } = true;

    // [ObservableProperty]
    // [SettingsSelectionItem(ItemsSourceBindingPath = "")]
    // public partial Guid TitleGeneratorAssistantId { get; set; }
    //
    // [ObservableProperty]
    // [SettingsStringItem(Watermark = Prompts.TitleGeneratorPrompt, IsMultiline = true, Height = 50)]
    // public partial Customizable<string> TitleGeneratorPromptTemplate { get; set; } = Prompts.TitleGeneratorPrompt;
}