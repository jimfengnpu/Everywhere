using Avalonia.Controls.Primitives;
using Everywhere.Interop;

namespace Everywhere.Views;

public class VisualElementPickerToolTip : TemplatedControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<VisualElementPickerToolTip, string?>(nameof(Header));

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<ElementPickMode> ModeProperty =
        AvaloniaProperty.Register<VisualElementPickerToolTip, ElementPickMode>(nameof(Mode));

    public ElementPickMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }
}