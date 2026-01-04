using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Everywhere.AI;
using Everywhere.Configuration;

namespace Everywhere.Views;

public class ModelProviderConfiguratorSelector : TemplatedControl
{
    public sealed record ConfiguratorModel(
        ModelProviderConfiguratorType Type,
        DynamicResourceKeyBase HeaderKey,
        DynamicResourceKeyBase DescriptionKey,
        IBrush? Background);

    public IReadOnlyList<ConfiguratorModel> ConfiguratorModels { get; } =
    [
        new ConfiguratorModel(
            ModelProviderConfiguratorType.PresetBased,
            new DynamicResourceKey(LocaleKey.ModelProviderConfiguratorSelector_PresetBasedConfiguratorModel_Header),
            new DynamicResourceKey(LocaleKey.ModelProviderConfiguratorSelector_PresetBasedConfiguratorModel_Description),
            null),
        new ConfiguratorModel(
            ModelProviderConfiguratorType.Advanced,
            new DynamicResourceKey(LocaleKey.ModelProviderConfiguratorSelector_AdvancedConfiguratorModel_Header),
            new DynamicResourceKey(LocaleKey.ModelProviderConfiguratorSelector_AdvancedConfiguratorModel_Description),
            null),
    ];

    /// <summary>
    /// Defines the <see cref="SelectedType"/> property.
    /// </summary>
    public static readonly StyledProperty<ModelProviderConfiguratorType> SelectedTypeProperty =
        AvaloniaProperty.Register<ModelProviderConfiguratorSelector, ModelProviderConfiguratorType>(nameof(SelectedType));

    /// <summary>
    /// Gets or sets the selected model provider configurator type.
    /// </summary>
    public ModelProviderConfiguratorType SelectedType
    {
        get => GetValue(SelectedTypeProperty);
        set => SetValue(SelectedTypeProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="SettingsItems"/> property.
    /// </summary>
    public static readonly StyledProperty<SettingsItems> SettingsItemsProperty =
        AvaloniaProperty.Register<ModelProviderConfiguratorSelector, SettingsItems>(nameof(SettingsItems));

    /// <summary>
    /// Gets or sets the settings items to be displayed.
    /// </summary>
    public SettingsItems SettingsItems
    {
        get => GetValue(SettingsItemsProperty);
        set => SetValue(SettingsItemsProperty, value);
    }
}