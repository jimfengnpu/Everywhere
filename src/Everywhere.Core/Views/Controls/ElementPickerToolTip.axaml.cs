using Avalonia.Controls.Primitives;
using Everywhere.Interop;

namespace Everywhere.Views;

public class ElementPickerToolTip : TemplatedControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<ElementPickerToolTip, string?>(nameof(Header));

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<PickElementMode> ModeProperty =
        AvaloniaProperty.Register<ElementPickerToolTip, PickElementMode>(nameof(Mode));

    public PickElementMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }
}