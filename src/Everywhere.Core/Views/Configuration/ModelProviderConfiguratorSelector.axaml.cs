using Avalonia.Controls.Primitives;
using Everywhere.AI;
using Everywhere.Configuration;
using System;
using System.Collections.Generic;

namespace Everywhere.Views;

public class ModelProviderConfiguratorSelector : TemplatedControl
{
    /// <summary>
    /// Gets the available model provider configurator types.
    /// </summary>
    public IEnumerable<ModelProviderConfiguratorType> AvailableTypes { get; } = Enum.GetValues<ModelProviderConfiguratorType>();

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