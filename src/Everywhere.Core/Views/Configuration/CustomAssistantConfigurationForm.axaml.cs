using Avalonia.Controls.Primitives;
using Everywhere.AI;

namespace Everywhere.Views;

public class CustomAssistantConfigurationForm : TemplatedControl
{
    /// <summary>
    /// Defines the <see cref="CustomAssistant"/> property.
    /// </summary>
    public static readonly StyledProperty<CustomAssistant?> CustomAssistantProperty =
        AvaloniaProperty.Register<CustomAssistantConfigurationForm, CustomAssistant?>(nameof(CustomAssistant));

    /// <summary>
    /// Gets or sets the CustomAssistant to configure.
    /// </summary>
    public CustomAssistant? CustomAssistant
    {
        get => GetValue(CustomAssistantProperty);
        set => SetValue(CustomAssistantProperty, value);
    }
}