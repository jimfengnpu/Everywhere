using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Microsoft.Extensions.Logging;

namespace Everywhere.ValueConverters;

public class LogLevelToBrushConverter : AvaloniaObject, IValueConverter
{
    public static readonly StyledProperty<IBrush?> TraceBrushProperty =
        AvaloniaProperty.Register<LogLevelToBrushConverter, IBrush?>(nameof(TraceBrush));

    public IBrush? TraceBrush
    {
        get => GetValue(TraceBrushProperty);
        set => SetValue(TraceBrushProperty, value);
    }

    public static readonly StyledProperty<IBrush?> DebugBrushProperty =
        AvaloniaProperty.Register<LogLevelToBrushConverter, IBrush?>(nameof(DebugBrush));

    public IBrush? DebugBrush
    {
        get => GetValue(DebugBrushProperty);
        set => SetValue(DebugBrushProperty, value);
    }

    public static readonly StyledProperty<IBrush?> InformationBrushProperty =
        AvaloniaProperty.Register<LogLevelToBrushConverter, IBrush?>(nameof(InformationBrush));

    public IBrush? InformationBrush
    {
        get => GetValue(InformationBrushProperty);
        set => SetValue(InformationBrushProperty, value);
    }

    public static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<LogLevelToBrushConverter, IBrush?>(nameof(WarningBrush));

    public IBrush? WarningBrush
    {
        get => GetValue(WarningBrushProperty);
        set => SetValue(WarningBrushProperty, value);
    }

    public static readonly StyledProperty<IBrush?> ErrorBrushProperty =
        AvaloniaProperty.Register<LogLevelToBrushConverter, IBrush?>(nameof(ErrorBrush));

    public IBrush? ErrorBrush
    {
        get => GetValue(ErrorBrushProperty);
        set => SetValue(ErrorBrushProperty, value);
    }

    public static readonly StyledProperty<IBrush?> CriticalBrushProperty =
        AvaloniaProperty.Register<LogLevelToBrushConverter, IBrush?>(nameof(CriticalBrush));

    public IBrush? CriticalBrush
    {
        get => GetValue(CriticalBrushProperty);
        set => SetValue(CriticalBrushProperty, value);
    }

    public static readonly StyledProperty<IBrush?> DefaultBrushProperty =
        AvaloniaProperty.Register<LogLevelToBrushConverter, IBrush?>(nameof(DefaultBrush));

    public IBrush? DefaultBrush
    {
        get => GetValue(DefaultBrushProperty);
        set => SetValue(DefaultBrushProperty, value);
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value switch
        {
            LogLevel.Trace => TraceBrush,
            LogLevel.Debug => DebugBrush,
            LogLevel.Information => InformationBrush,
            LogLevel.Warning => WarningBrush,
            LogLevel.Error => ErrorBrush,
            LogLevel.Critical => CriticalBrush,
            _ => null
        };

        return result ?? DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}