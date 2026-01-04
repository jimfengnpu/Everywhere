using System.ComponentModel.DataAnnotations;
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
public partial class CustomAssistant : ObservableValidator
{
    [HiddenSettingsItem]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial ColoredIcon? Icon { get; set; } = new(ColoredIconType.Lucide) { Kind = LucideIconKind.Bot };

    [ObservableProperty]
    [HiddenSettingsItem]
    [MinLength(1)]
    [MaxLength(128)]
    public required partial string Name { get; set; }

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial string? Description { get; set; }

    [DynamicResourceKey(LocaleKey.Empty)]
    public SettingsControl<CustomAssistantInformationForm> InformationForm => new(
        new CustomAssistantInformationForm
        {
            CustomAssistant = this
        });

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
            if (field == value) return;

            Configurator.Backup();
            field = value;
            Configurator.Apply();

            OnPropertyChanged();
            OnPropertyChanged(nameof(Configurator));
        }
    }

    [JsonIgnore]
    [HiddenSettingsItem]
    public IModelProviderConfigurator Configurator => ConfiguratorType switch
    {
        ModelProviderConfiguratorType.Official => _officialConfigurator,
        ModelProviderConfiguratorType.PresetBased => _presetBasedConfigurator,
        _ => _advancedConfigurator
    };

    [JsonIgnore]
    [DynamicResourceKey(LocaleKey.CustomAssistant_ConfiguratorSelector_Header)]
    public SettingsControl<ModelProviderConfiguratorSelector> ConfiguratorSelector => new(
        new ModelProviderConfiguratorSelector
        {
            [!ModelProviderConfiguratorSelector.SelectedTypeProperty] = new Binding(nameof(ConfiguratorType))
            {
                Source = this,
                Mode = BindingMode.TwoWay
            },
            [!ModelProviderConfiguratorSelector.SettingsItemsProperty] = new Binding($"{nameof(Configurator)}.{nameof(Configurator.SettingsItems)}")
            {
                Source = this
            },
        });

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial string? Endpoint { get; set; }

    /// <summary>
    /// The GUID of the API key to use for this custom assistant.
    /// Use string? for forward compatibility.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial Guid ApiKey { get; set; }

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial ModelProviderSchema Schema { get; set; }

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial string? ModelProviderTemplateId { get; set; }

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial string? ModelDefinitionTemplateId { get; set; }

    [ObservableProperty]
    [HiddenSettingsItem]
    public partial string? ModelId { get; set; }

    /// <summary>
    /// Indicates whether the model supports image input capabilities.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial bool IsImageInputSupported { get; set; }

    /// <summary>
    /// Indicates whether the model supports function calling capabilities.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial bool IsFunctionCallingSupported { get; set; }

    /// <summary>
    /// Indicates whether the model supports tool calls.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial bool IsDeepThinkingSupported { get; set; }

    /// <summary>
    /// Maximum number of tokens that the model can process in a single request.
    /// aka, the maximum context length.
    /// </summary>
    [ObservableProperty]
    [HiddenSettingsItem]
    public partial int MaxTokens { get; set; }

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
    PresetBased,
    Official,
}

public interface IModelProviderConfigurator
{
    [HiddenSettingsItem]
    SettingsItems SettingsItems { get; }

    /// <summary>
    /// Called before switching to another configurator type to backup necessary values.
    /// </summary>
    void Backup();

    /// <summary>
    /// Called to apply the configuration to the associated CustomAssistant.
    /// </summary>
    void Apply();

    /// <summary>
    /// Validate the current configuration and show UI feedback if invalid.
    /// </summary>
    /// <returns>
    /// True if the configuration is valid; otherwise, false.
    /// </returns>
    bool Validate();
}

/// <summary>
/// Configurator for the Everywhere official model provider.
/// </summary>
[GeneratedSettingsItems]
public sealed partial class OfficialModelProviderConfigurator(CustomAssistant owner) : ObservableValidator, IModelProviderConfigurator
{
    public void Backup()
    {
    }

    public void Apply()
    {
    }

    public bool Validate()
    {
        ValidateAllProperties();
        return !HasErrors;
    }
}

/// <summary>
/// Configurator for preset-based model providers.
/// </summary>
[GeneratedSettingsItems]
public sealed partial class PresetBasedModelProviderConfigurator(CustomAssistant owner) : ObservableValidator, IModelProviderConfigurator
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
        get => owner.ModelProviderTemplateId;
        set
        {
            if (value == owner.ModelProviderTemplateId) return;
            owner.ModelProviderTemplateId = value;

            Apply();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModelProviderTemplate));
            OnPropertyChanged(nameof(ModelDefinitionTemplates));
        }
    }

    [Required]
    [JsonIgnore]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_ModelProviderTemplate_Header,
        LocaleKey.CustomAssistant_ModelProviderTemplate_Description)]
    [SettingsSelectionItem(nameof(ModelProviderTemplates), DataTemplateKey = typeof(ModelProviderTemplate))]
    public ModelProviderTemplate? ModelProviderTemplate
    {
        get => ModelProviderTemplates.FirstOrDefault(t => t.Id == ModelProviderTemplateId);
        set => ModelProviderTemplateId = value?.Id;
    }

    [HiddenSettingsItem]
    public Guid ApiKey
    {
        get => owner.ApiKey;
        set
        {
            if (owner.ApiKey == value) return;

            owner.ApiKey = value;
            _apiKeyBackup = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_ApiKey_Header,
        LocaleKey.CustomAssistant_ApiKey_Description)]
    public SettingsControl<ApiKeyComboBox> ApiKeyControl => new(
        new ApiKeyComboBox(ServiceLocator.Resolve<Settings>().Model.ApiKeys)
        {
            [!ApiKeyComboBox.SelectedIdProperty] = new Binding(nameof(ApiKey))
            {
                Source = this,
                Mode = BindingMode.TwoWay
            },
            [!ApiKeyComboBox.DefaultNameProperty] = new Binding($"{nameof(ModelProviderTemplate)}.{nameof(ModelProviderTemplate.DisplayName)}")
            {
                Source = this,
            },
        });

    [JsonIgnore]
    [HiddenSettingsItem]
    private IEnumerable<ModelDefinitionTemplate> ModelDefinitionTemplates => ModelProviderTemplate?.ModelDefinitions ?? [];

    [HiddenSettingsItem]
    public string? ModelDefinitionTemplateId
    {
        get => owner.ModelDefinitionTemplateId;
        set
        {
            if (value == owner.ModelDefinitionTemplateId) return;
            owner.ModelDefinitionTemplateId = value;

            Apply();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModelDefinitionTemplate));
        }
    }

    [Required]
    [JsonIgnore]
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

    private Guid _apiKeyBackup;

    public void Backup()
    {
        _apiKeyBackup = owner.ApiKey;
    }

    public void Apply()
    {
        owner.ApiKey = _apiKeyBackup;

        var modelProviderTemplate = ModelProviderTemplates.FirstOrDefault(t => t.Id == ModelProviderTemplateId);
        if (modelProviderTemplate is not null)
        {
            owner.Endpoint = modelProviderTemplate.Endpoint;
            owner.Schema = modelProviderTemplate.Schema;
            owner.RequestTimeoutSeconds = modelProviderTemplate.RequestTimeoutSeconds;
            ModelDefinitionTemplateId = modelProviderTemplate.ModelDefinitions.FirstOrDefault(m => m.IsDefault)?.Id;
        }
        else
        {
            owner.Endpoint = string.Empty;
            owner.Schema = ModelProviderSchema.OpenAI;
            owner.RequestTimeoutSeconds = 20;
            ModelDefinitionTemplateId = null;
        }

        var modelDefinitionTemplate = modelProviderTemplate?.ModelDefinitions.FirstOrDefault(m => m.Id == ModelDefinitionTemplateId);
        if (modelDefinitionTemplate is not null)
        {
            owner.ModelId = modelDefinitionTemplate.Id;
            owner.IsImageInputSupported = modelDefinitionTemplate.IsImageInputSupported;
            owner.IsFunctionCallingSupported = modelDefinitionTemplate.IsFunctionCallingSupported;
            owner.IsDeepThinkingSupported = modelDefinitionTemplate.IsDeepThinkingSupported;
            owner.MaxTokens = modelDefinitionTemplate.MaxTokens;
        }
        else
        {
            owner.ModelId = string.Empty;
            owner.IsImageInputSupported = false;
            owner.IsFunctionCallingSupported = false;
            owner.IsDeepThinkingSupported = false;
            owner.MaxTokens = 81920;
        }
    }

    public bool Validate()
    {
        ValidateAllProperties();
        return !HasErrors;
    }
}

/// <summary>
/// Configurator for advanced model providers.
/// </summary>
[GeneratedSettingsItems]
public sealed partial class AdvancedModelProviderConfigurator(CustomAssistant owner) : ObservableValidator, IModelProviderConfigurator
{
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_Endpoint_Header,
        LocaleKey.CustomAssistant_Endpoint_Description)]
    [CustomValidation(typeof(AdvancedModelProviderConfigurator), nameof(ValidateEndpoint))]
    public string? Endpoint
    {
        get => owner.Endpoint;
        set => owner.Endpoint = value;
    }

    [HiddenSettingsItem]
    public Guid ApiKey
    {
        get => owner.ApiKey;
        set
        {
            if (owner.ApiKey == value) return;

            owner.ApiKey = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_ApiKey_Header,
        LocaleKey.CustomAssistant_ApiKey_Description)]
    public SettingsControl<ApiKeyComboBox> ApiKeyControl => new(
        new ApiKeyComboBox(ServiceLocator.Resolve<Settings>().Model.ApiKeys)
        {
            [!ApiKeyComboBox.SelectedIdProperty] = new Binding(nameof(ApiKey))
            {
                Source = this,
                Mode = BindingMode.TwoWay
            },
        });

    [DynamicResourceKey(
        LocaleKey.CustomAssistant_Schema_Header,
        LocaleKey.CustomAssistant_Schema_Description)]
    public ModelProviderSchema Schema
    {
        get => owner.Schema;
        set => owner.Schema = value;
    }

    [DynamicResourceKey(
        LocaleKey.CustomAssistant_ModelId_Header,
        LocaleKey.CustomAssistant_ModelId_Description)]
    [Required, MinLength(1)]
    public string? ModelId
    {
        get => owner.ModelId;
        set => owner.ModelId = value;
    }

    /// <summary>
    /// Indicates whether the model supports image input capabilities.
    /// </summary>
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_IsImageInputSupported_Header,
        LocaleKey.CustomAssistant_IsImageInputSupported_Description)]
    public bool IsImageInputSupported
    {
        get => owner.IsImageInputSupported;
        set => owner.IsImageInputSupported = value;
    }

    /// <summary>
    /// Indicates whether the model supports function calling capabilities.
    /// </summary>
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_IsFunctionCallingSupported_Header,
        LocaleKey.CustomAssistant_IsFunctionCallingSupported_Description)]
    public bool IsFunctionCallingSupported
    {
        get => owner.IsFunctionCallingSupported;
        set => owner.IsFunctionCallingSupported = value;
    }

    /// <summary>
    /// Indicates whether the model supports tool calls.
    /// </summary>
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_IsDeepThinkingSupported_Header,
        LocaleKey.CustomAssistant_IsDeepThinkingSupported_Description)]
    public bool IsDeepThinkingSupported
    {
        get => owner.IsDeepThinkingSupported;
        set => owner.IsDeepThinkingSupported = value;
    }

    /// <summary>
    /// Maximum number of tokens that the model can process in a single request.
    /// aka, the maximum context length.
    /// </summary>
    [DynamicResourceKey(
        LocaleKey.CustomAssistant_MaxTokens_Header,
        LocaleKey.CustomAssistant_MaxTokens_Description)]
    [SettingsIntegerItem(IsSliderVisible = false)]
    public int MaxTokens
    {
        get => owner.MaxTokens;
        set => owner.MaxTokens = value;
    }

    /// <summary>
    /// Backups of the original customizable values before switching to advanced configurator.
    /// Key: Property name
    /// Value: (DefaultValue, CustomValue)
    /// </summary>
    private readonly Dictionary<string, object?> _backups = new();

    public void Backup()
    {
        Backup(Endpoint);
        Backup(Schema);
        Backup(ModelId);
        Backup(IsImageInputSupported);
        Backup(IsFunctionCallingSupported);
        Backup(IsDeepThinkingSupported);
        Backup(MaxTokens);
    }

    public void Apply()
    {
        Endpoint = Restore(Endpoint);
        Schema = Restore(Schema);
        ModelId = Restore(ModelId);
        IsImageInputSupported = Restore(IsImageInputSupported);
        IsFunctionCallingSupported = Restore(IsFunctionCallingSupported);
        IsDeepThinkingSupported = Restore(IsDeepThinkingSupported);
        MaxTokens = Restore(MaxTokens);
    }

    public bool Validate()
    {
        ValidateAllProperties();
        return !HasErrors;
    }

    /// <summary>
    /// When the user switches configurator types, we need to preserve the values set in the advanced configurator.
    /// This method helps to return the original customizable, while keeping a backup if needed.
    /// </summary>
    /// <param name="property"></param>
    /// <param name="propertyName"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private void Backup<T>(T property, [CallerArgumentExpression("property")] string propertyName = "")
    {
        _backups[propertyName] = property;
    }

    private T? Restore<T>(T property, [CallerArgumentExpression("property")] string propertyName = "")
    {
        return _backups.TryGetValue(propertyName, out var backup) ? (T?)backup : default;
    }

    public static ValidationResult? ValidateEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new ValidationResult(LocaleResolver.ValidationErrorMessage_Required);
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return new ValidationResult(LocaleResolver.AdvancedModelProviderConfigurator_InvalidEndpoint);
        }

        return ValidationResult.Success;
    }
}

[JsonSerializable(typeof(CustomAssistant))]
public partial class CustomAssistantJsonSerializerContext : JsonSerializerContext;