using Avalonia.Controls;
using Everywhere.Interop;

namespace Everywhere.Views;

public class ElementPickerToolTipWindow : Window
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<ElementPickerToolTipWindow, string?>(nameof(Header));

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<PickElementMode> ModeProperty =
        AvaloniaProperty.Register<ElementPickerToolTipWindow, PickElementMode>(nameof(Mode));

    public PickElementMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }
}