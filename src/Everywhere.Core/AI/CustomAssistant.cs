using System.ComponentModel;
using System.Text.Json.Serialization;
using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Views;
using Lucide.Avalonia;

namespace Everywhere.AI;

/// <summary>
/// Allowing users to define and manage their own custom AI assistants.
/// </summary>
[GeneratedSettingsItems]
public partial class CustomAssistant : ObservableObject
{
    [HiddenSettingsItem]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_Icon_Header,
        LocaleKey.CustomAssistant_Icon_Description)]
    [SettingsTemplatedItem]
    public partial ColoredIcon? Icon { get; set; } = new(ColoredIconType.Lucide) { Kind = LucideIconKind.Bot };

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_Name_Header,
        LocaleKey.CustomAssistant_Name_Description)]
    [SettingsStringItem(MaxLength = 32)]
    public required partial string Name { get; set; }

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_Description_Header,
        LocaleKey.CustomAssistant_Description_Description)]
    [SettingsStringItem(IsMultiline = true, MaxLength = 4096, Height = 80)]
    public partial string? Description { get; set; }

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_SystemPrompt_Header,
        LocaleKey.CustomAssistant_SystemPrompt_Description)]
    [SettingsStringItem(IsMultiline = true, MaxLength = 40960)]
    public partial Customizable<string> SystemPrompt { get; set; } = new(Prompts.DefaultSystemPrompt, isDefaultValueReadonly: true);

    [HiddenSettingsItem]
    public ModelProviderConfiguratorType ConfiguratorType
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;

            Configurator.Apply();
            OnPropertyChanged(nameof(Configurator));
        }
    }

    [JsonIgnore]
    [HiddenSettingsItem]
    public IModelProviderConfigurator Configurator => ConfiguratorType switch
    {
        ModelProviderConfiguratorType.Official => _officialConfigurator,
        ModelProviderConfiguratorType.Templated => _presetBasedConfigurator,
        _ => _advancedConfigurator
    };

    [JsonIgnore]
    [DynamicResourceKey(LocaleKey.CustomAssistant_ConfiguratorSelector_Header)]
    public SettingsControl<ModelProviderConfiguratorSelector> ConfiguratorSelector => new(
        new ModelProviderConfiguratorSelector
        {
            [!ModelProviderConfiguratorSelector.SelectedTypeProperty] = new Binding(nameof(ConfiguratorType))
            {
                Source = this
            },
            [!ModelProviderConfiguratorSelector.SettingsItemsProperty] = new Binding($"{nameof(Configurator)}.{nameof(Configurator.SettingsItems)}")
            {
                Source = this
            },
        });

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string> Endpoint { get; set; } = string.Empty;

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<ModelProviderSchema> Schema { get; set; } = ModelProviderSchema.OpenAI;

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial string? ApiKey { get; set; }

    [HiddenSettingsItem]
    public string? ModelProviderTemplateId
    {
        get => _presetBasedConfigurator.ModelProviderTemplateId;
        set => _presetBasedConfigurator.ModelProviderTemplateId = value;
    }

    [HiddenSettingsItem]
    public string? ModelDefinitionTemplateId
    {
        get => _presetBasedConfigurator.ModelDefinitionTemplateId;
        set => _presetBasedConfigurator.ModelDefinitionTemplateId = value;
    }

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<string> ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the model supports image input capabilities.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<bool> IsImageInputSupported { get; set; } = false;

    /// <summary>
    /// Indicates whether the model supports function calling capabilities.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<bool> IsFunctionCallingSupported { get; set; } = false;

    /// <summary>
    /// Indicates whether the model supports tool calls.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<bool> IsDeepThinkingSupported { get; set; } = false;

    /// <summary>
    /// Maximum number of tokens that the model can process in a single request.
    /// aka, the maximum context length.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Customizable<int> MaxTokens { get; set; } = 81920;

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_RequestTimeoutSeconds_Header,
        LocaleKey.CustomAssistant_RequestTimeoutSeconds_Description)]
    [SettingsIntegerItem(IsSliderVisible = false)]
    public partial Customizable<int> RequestTimeoutSeconds { get; set; } = 20;

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_Temperature_Header,
        LocaleKey.CustomAssistant_Temperature_Description)]
    [SettingsDoubleItem(Min = 0.0, Max = 2.0, Step = 0.1)]
    public partial Customizable<double> Temperature { get; set; } = 1.0;

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_TopP_Header,
        LocaleKey.CustomAssistant_TopP_Description)]
    [SettingsDoubleItem(Min = 0.0, Max = 1.0, Step = 0.1)]
    public partial Customizable<double> TopP { get; set; } = 0.9;

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_PresencePenalty_Header,
        LocaleKey.CustomAssistant_PresencePenalty_Description)]
    [SettingsDoubleItem(Min = -2.0, Max = 2.0, Step = 0.1)]
    public partial Customizable<double> PresencePenalty { get; set; } = 0.0;

    [ObservableProperty]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_FrequencyPenalty_Header,
        LocaleKey.CustomAssistant_FrequencyPenalty_Description)]
    [SettingsDoubleItem(Min = -2.0, Max = 2.0, Step = 0.1)]
    public partial Customizable<double> FrequencyPenalty { get; set; } = 0.0;

    private readonly OfficialModelProviderConfigurator _officialConfigurator;
    private readonly PresetBasedModelProviderConfigurator _presetBasedConfigurator;
    private readonly AdvancedModelProviderConfigurator _advancedConfigurator;

    public CustomAssistant()
    {
        _officialConfigurator = new OfficialModelProviderConfigurator(this);
        _presetBasedConfigurator = new PresetBasedModelProviderConfigurator(this);
        _advancedConfigurator = new AdvancedModelProviderConfigurator(this);
    }
}

public enum ModelProviderConfiguratorType
{
    /// <summary>
    /// Advanced first for forward compatibility.
    /// </summary>
    Advanced,
    Templated,
    Official,
}

public interface IModelProviderConfigurator
{
    [HiddenSettingsItem]
    SettingsItems SettingsItems { get; }

    /// <summary>
    /// Called to apply the configuration to the associated CustomAssistant.
    /// </summary>
    void Apply();
}

/// <summary>
/// Configurator for the Everywhere official model provider.
/// </summary>
[GeneratedSettingsItems]
public partial class OfficialModelProviderConfigurator(CustomAssistant owner) : ObservableObject, IModelProviderConfigurator
{
    public void Apply()
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Configurator for preset-based model providers.
/// </summary>
[GeneratedSettingsItems]
public partial class PresetBasedModelProviderConfigurator(CustomAssistant owner) : ObservableObject, IModelProviderConfigurator
{
    /// <summary>
    /// Helper property to get all supported model provider templates.
    /// </summary>
    [JsonIgnore]
    [HiddenSettingsItem]
    private static ModelProviderTemplate[] ModelProviderTemplates => ModelProviderTemplate.SupportedTemplates;

    /// <summary>
    /// The ID of the model provider to use for this custom assistant.
    /// This ID should correspond to one of the available model providers in the application.
    /// </summary>
    [HiddenSettingsItem]
    public string? ModelProviderTemplateId
    {
        get;
        set
        {
            if (value == field) return;
            field = value;

            Apply();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModelProviderTemplate));
            OnPropertyChanged(nameof(ModelDefinitionTemplates));
        }
    }

    [JsonIgnore]
    [DefaultValue(null)]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_ModelProviderTemplate_Header,
        LocaleKey.CustomAssistant_ModelProviderTemplate_Description)]
    [SettingsSelectionItem(nameof(ModelProviderTemplates), DataTemplateKey = typeof(ModelProviderTemplate))]
    public ModelProviderTemplate? ModelProviderTemplate
    {
        get => ModelProviderTemplates.FirstOrDefault(t => t.Id == ModelProviderTemplateId);
        set => ModelProviderTemplateId = value?.Id;
    }

    [JsonIgnore]
    [HiddenSettingsItem]
    private IEnumerable<ModelDefinitionTemplate> ModelDefinitionTemplates => ModelProviderTemplate?.ModelDefinitions ?? [];

    [HiddenSettingsItem]
    public string? ModelDefinitionTemplateId
    {
        get;
        set
        {
            if (value == field) return;
            field = value;

            Apply();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModelDefinitionTemplate));
        }
    }

    [JsonIgnore]
    [DefaultValue(null)]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_ModelDefinitionTemplate_Header,
        LocaleKey.CustomAssistant_ModelDefinitionTemplate_Description)]
    [SettingsSelectionItem(nameof(ModelDefinitionTemplates), DataTemplateKey = typeof(ModelDefinitionTemplate))]
    public ModelDefinitionTemplate? ModelDefinitionTemplate
    {
        get => ModelProviderTemplates.FirstOrDefault(t => t.Id == ModelProviderTemplateId)?
            .ModelDefinitions.FirstOrDefault(m => m.Id == ModelDefinitionTemplateId);
        set => ModelDefinitionTemplateId = value?.Id;
    }

    [DynamicResourceKey(
        LocaleKey.CustomAssistant_ApiKey_Header,
        LocaleKey.CustomAssistant_ApiKey_Description)]
    [SettingsStringItem(IsPassword = true)]
    public string? ApiKey
    {
        get => owner.ApiKey;
        set
        {
            if (owner.ApiKey == value) return;

            owner.ApiKey = value;
            OnPropertyChanged();
        }
    }

    public void Apply()
    {
        if (owner.ConfiguratorType != ModelProviderConfiguratorType.Templated) return;

        var modelProviderTemplate = ModelProviderTemplates.FirstOrDefault(t => t.Id == ModelProviderTemplateId);
        if (modelProviderTemplate is not null)
        {
            ApplyCustomizable(owner.Endpoint, modelProviderTemplate.Endpoint);
            ApplyCustomizable(owner.Schema, modelProviderTemplate.Schema);
            ApplyCustomizable(owner.RequestTimeoutSeconds, modelProviderTemplate.RequestTimeoutSeconds);
            ModelDefinitionTemplateId = modelProviderTemplate.ModelDefinitions.FirstOrDefault(m => m.IsDefault)?.Id;
        }
        else
        {
            ApplyCustomizable(owner.Endpoint, string.Empty);
            ApplyCustomizable(owner.Schema, ModelProviderSchema.OpenAI);
            ApplyCustomizable(owner.RequestTimeoutSeconds, 20);
            ModelDefinitionTemplateId = null;
        }

        var modelDefinitionTemplate = modelProviderTemplate?.ModelDefinitions.FirstOrDefault(m => m.Id == ModelDefinitionTemplateId);
        if (modelDefinitionTemplate is not null)
        {
            ApplyCustomizable(owner.ModelId, modelDefinitionTemplate.Id);
            ApplyCustomizable(owner.IsImageInputSupported, modelDefinitionTemplate.IsImageInputSupported);
            ApplyCustomizable(owner.IsFunctionCallingSupported, modelDefinitionTemplate.IsFunctionCallingSupported);
            ApplyCustomizable(owner.IsDeepThinkingSupported, modelDefinitionTemplate.IsDeepThinkingSupported);
            ApplyCustomizable(owner.MaxTokens, modelDefinitionTemplate.MaxTokens);
        }
        else
        {
            ApplyCustomizable(owner.ModelId, string.Empty);
            ApplyCustomizable(owner.IsImageInputSupported, false);
            ApplyCustomizable(owner.IsFunctionCallingSupported, false);
            ApplyCustomizable(owner.IsDeepThinkingSupported, false);
            ApplyCustomizable(owner.MaxTokens, 81920);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyCustomizable<T>(Customizable<T> customizable, T value) where T : notnull
    {
        customizable.DefaultValue = value;
        customizable.CustomValue = value;
    }
}

/// <summary>
/// Configurator for advanced model providers.
/// </summary>
[GeneratedSettingsItems]
public partial class AdvancedModelProviderConfigurator(CustomAssistant owner) : ObservableObject, IModelProviderConfigurator
{
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_Endpoint_Header,
        LocaleKey.CustomAssistant_Endpoint_Description)]
    public Customizable<string> Endpoint => BackupThenReturn(owner.Endpoint);

    [DynamicResourceKey(
        LocaleKey.CustomAssistant_Schema_Header,
        LocaleKey.CustomAssistant_Schema_Description)]
    public Customizable<ModelProviderSchema> Schema => BackupThenReturn(owner.Schema);

    [DynamicResourceKey(
        LocaleKey.CustomAssistant_ApiKey_Header,
        LocaleKey.CustomAssistant_ApiKey_Description)]
    [SettingsStringItem(IsPassword = true)]
    public string? ApiKey
    {
        get => owner.ApiKey;
        set
        {
            if (owner.ApiKey == value) return;

            owner.ApiKey = value;
            OnPropertyChanged();
        }
    }

    [DynamicResourceKey(
        LocaleKey.CustomAssistant_ModelId_Header,
        LocaleKey.CustomAssistant_ModelId_Description)]
    public Customizable<string> ModelId => BackupThenReturn(owner.ModelId);

    /// <summary>
    /// Indicates whether the model supports image input capabilities.
    /// </summary>
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_IsImageInputSupported_Header,
        LocaleKey.CustomAssistant_IsImageInputSupported_Description)]
    public Customizable<bool> IsImageInputSupported => BackupThenReturn(owner.IsImageInputSupported);

    /// <summary>
    /// Indicates whether the model supports function calling capabilities.
    /// </summary>
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_IsFunctionCallingSupported_Header,
        LocaleKey.CustomAssistant_IsFunctionCallingSupported_Description)]
    public Customizable<bool> IsFunctionCallingSupported => BackupThenReturn(owner.IsFunctionCallingSupported);

    /// <summary>
    /// Indicates whether the model supports tool calls.
    /// </summary>
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_IsDeepThinkingSupported_Header,
        LocaleKey.CustomAssistant_IsDeepThinkingSupported_Description)]
    public Customizable<bool> IsDeepThinkingSupported => BackupThenReturn(owner.IsDeepThinkingSupported);

    /// <summary>
    /// Maximum number of tokens that the model can process in a single request.
    /// aka, the maximum context length.
    /// </summary>
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_MaxTokens_Header,
        LocaleKey.CustomAssistant_MaxTokens_Description)]
    [SettingsIntegerItem(IsSliderVisible = false)]
    public Customizable<int> MaxTokens => BackupThenReturn(owner.MaxTokens);

    /// <summary>
    /// Backups of the original customizable values before switching to advanced configurator.
    /// Key: Property name
    /// Value: (DefaultValue, CustomValue)
    /// </summary>
    private readonly Dictionary<string, (object, object?)> _backups = new();

    public void Apply()
    {
        Restore(Endpoint);
        Restore(Schema);
        Restore(ModelId);
        Restore(IsImageInputSupported);
        Restore(IsFunctionCallingSupported);
        Restore(IsDeepThinkingSupported);
        Restore(MaxTokens);
    }

    /// <summary>
    /// When the user switches configurator types, we need to preserve the values set in the advanced configurator.
    /// This method helps to return the original customizable, while keeping a backup if needed.
    /// </summary>
    /// <param name="customizable"></param>
    /// <param name="propertyName"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private Customizable<T> BackupThenReturn<T>(Customizable<T> customizable, [CallerMemberName] string propertyName = "") where T : notnull
    {
        _backups[propertyName] = (customizable.DefaultValue, customizable.CustomValue);
        return customizable;
    }

    private void Restore<T>(Customizable<T> customizable, [CallerMemberName] string propertyName = "") where T : notnull
    {
        if (!_backups.TryGetValue(propertyName, out var backup)) return;
        customizable.DefaultValue = (T)backup.Item1;
        customizable.CustomValue = (T?)backup.Item2;
    }
}

[JsonSerializable(typeof(CustomAssistant))]
public partial class CustomAssistantJsonSerializerContext : JsonSerializerContext;