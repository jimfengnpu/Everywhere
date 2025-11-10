using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using ZLinq;

namespace Everywhere.Configuration;

/// <summary>
/// Represents a single settings item for View.
/// </summary>
public class SettingsItem : AvaloniaObject
{
    public DynamicResourceKey? HeaderKey { get; set; }

    public DynamicResourceKey? DescriptionKey { get; set; }

    public static readonly StyledProperty<object?> ValueProperty = AvaloniaProperty.Register<SettingsItem, object?>(nameof(Value));

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<bool> IsEnabledProperty = AvaloniaProperty.Register<SettingsItem, bool>(nameof(IsEnabled), true);

    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public static readonly StyledProperty<bool> IsVisibleProperty = AvaloniaProperty.Register<SettingsItem, bool>(nameof(IsVisible), true);

    public bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<SettingsItem, bool>(nameof(IsExpanded));

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public static readonly StyledProperty<bool> IsExpandableProperty = AvaloniaProperty.Register<SettingsItem, bool>(nameof(IsExpandable));

    public bool IsExpandable
    {
        get => GetValue(IsExpandableProperty);
        set => SetValue(IsExpandableProperty, value);
    }

    public List<SettingsItem> Items { get; } = [];
}

public class SettingsBooleanItem : SettingsItem
{
    public static readonly StyledProperty<bool> IsNullableProperty = AvaloniaProperty.Register<SettingsBooleanItem, bool>(nameof(IsNullable));

    public bool IsNullable
    {
        get => GetValue(IsNullableProperty);
        set => SetValue(IsNullableProperty, value);
    }
}

public class SettingsStringItem : SettingsItem
{
    public static readonly StyledProperty<string?> WatermarkProperty = AvaloniaProperty.Register<SettingsStringItem, string?>(nameof(Watermark));

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public static readonly StyledProperty<int> MaxLengthProperty = AvaloniaProperty.Register<SettingsStringItem, int>(nameof(MaxLength));

    public int MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public static readonly StyledProperty<bool> IsMultilineProperty = AvaloniaProperty.Register<SettingsStringItem, bool>(nameof(IsMultiline));

    public bool IsMultiline
    {
        get => GetValue(IsMultilineProperty);
        set => SetValue(IsMultilineProperty, value);
    }

    public static readonly StyledProperty<TextWrapping> TextWrappingProperty = AvaloniaProperty.Register<SettingsStringItem, TextWrapping>(nameof(TextWrapping));

    public TextWrapping TextWrapping
    {
        get => GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public static readonly StyledProperty<char> PasswordCharProperty = AvaloniaProperty.Register<SettingsStringItem, char>(nameof(PasswordChar));

    public char PasswordChar
    {
        get => GetValue(PasswordCharProperty);
        set => SetValue(PasswordCharProperty, value);
    }

    public static readonly StyledProperty<double> HeightProperty = AvaloniaProperty.Register<SettingsStringItem, double>(nameof(Height));

    public double Height
    {
        get => GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }
}

public class SettingsIntegerItem : SettingsItem
{
    public static readonly StyledProperty<int> MinValueProperty = AvaloniaProperty.Register<SettingsIntegerItem, int>(nameof(MinValue));

    public int MinValue
    {
        get => GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public static readonly StyledProperty<int> MaxValueProperty = AvaloniaProperty.Register<SettingsIntegerItem, int>(nameof(MaxValue));

    public int MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public static readonly StyledProperty<bool> IsSliderVisibleProperty =
        AvaloniaProperty.Register<SettingsIntegerItem, bool>(nameof(IsSliderVisible));

    public bool IsSliderVisible
    {
        get => GetValue(IsSliderVisibleProperty);
        set => SetValue(IsSliderVisibleProperty, value);
    }
}

public class SettingsDoubleItem : SettingsItem
{
    public static readonly StyledProperty<double> MinValueProperty = AvaloniaProperty.Register<SettingsDoubleItem, double>(nameof(MinValue));

    public double MinValue
    {
        get => GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public static readonly StyledProperty<double> MaxValueProperty = AvaloniaProperty.Register<SettingsDoubleItem, double>(nameof(MaxValue));

    public double MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public static readonly StyledProperty<double> StepProperty = AvaloniaProperty.Register<SettingsDoubleItem, double>(nameof(Step));

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public static readonly StyledProperty<bool> IsSliderVisibleProperty =
        AvaloniaProperty.Register<SettingsDoubleItem, bool>(nameof(IsSliderVisible));

    public bool IsSliderVisible
    {
        get => GetValue(IsSliderVisibleProperty);
        set => SetValue(IsSliderVisibleProperty, value);
    }
}

public class SettingsSelectionItem : SettingsItem
{
    public record Item(DynamicResourceKey Key, object Value, IDataTemplate? ContentTemplate);

    public static readonly StyledProperty<IEnumerable<Item>> ItemsSourceProperty =
        AvaloniaProperty.Register<SettingsSelectionItem, IEnumerable<Item>>(nameof(ItemsSource));

    public IEnumerable<Item> ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DirectProperty<SettingsSelectionItem, Item?> SelectedItemProperty =
        AvaloniaProperty.RegisterDirect<SettingsSelectionItem, Item?>(
            nameof(SelectedItem),
            o => o.SelectedItem,
            (o, v) => o.SelectedItem = v);

    public Item? SelectedItem
    {
        get;
        set
        {
            if (Equals(field, value)) return;
            var oldValue = field;
            field = value;
            RaisePropertyChanged(SelectedItemProperty, oldValue, value);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty && ItemsSource.AsValueEnumerable().Count() > 0)
        {
            SelectedItem = ItemsSource.FirstOrDefault(i => Equals(i.Value, change.NewValue));
        }
        else if (change.Property == ItemsSourceProperty)
        {
            SelectedItem = change.NewValue.As<IEnumerable<Item>>()?.FirstOrDefault(i => Equals(i.Value, Value));
        }
        else if (change.Property == SelectedItemProperty)
        {
            Value = change.NewValue.As<Item>()?.Value;
        }
    }
}

public class SettingsCustomizableItem(SettingsItem customValueItem) : SettingsItem
{
    public SettingsItem CustomValueItem => customValueItem;

    public static readonly StyledProperty<ICommand?> ResetCommandProperty =
        AvaloniaProperty.Register<SettingsCustomizableItem, ICommand?>(nameof(ResetCommand));

    public ICommand? ResetCommand
    {
        get => GetValue(ResetCommandProperty);
        set => SetValue(ResetCommandProperty, value);
    }
}

/// <summary>
/// A settings item that holds a value of a specific type.
/// TType is used for DataTemplate selection.
/// </summary>
/// <param name="dataTemplate"></param>
public abstract class SettingsTypedItem(IDataTemplate? dataTemplate) : SettingsItem
{
    public IDataTemplate? DataTemplate => dataTemplate;

    public static SettingsTypedItem? TryCreate(Type propertyType)
    {
        if (Application.Current?.Resources.TryGetResource(propertyType, null, out var resource) is not true ||
            resource is not IDataTemplate dataTemplate)
        {
            return null;
        }

        var typedItem = typeof(SettingsTypedItem<>).MakeGenericType(propertyType);
        var constructor = typedItem.GetConstructor([typeof(string), typeof(IDataTemplate)]);
        return (SettingsTypedItem?)constructor?.Invoke([dataTemplate]);
    }
}

/// <summary>
/// A settings item that holds a value of a specific type.
/// TType is used for DataTemplate selection.
/// </summary>
/// <typeparam name="TType"></typeparam>
public class SettingsTypedItem<TType>(IDataTemplate? dataTemplate) : SettingsTypedItem(dataTemplate);

/// <summary>
/// A settings item that contains a custom control.
/// </summary>
/// <param name="control"></param>
public class SettingsControlItem(Control control) : SettingsItem
{
    public Control Control => control;
}