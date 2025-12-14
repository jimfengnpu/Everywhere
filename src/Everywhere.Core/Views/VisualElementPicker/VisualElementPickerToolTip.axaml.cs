using System.Diagnostics;
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

    public IVisualElement? Element
    {
        set => Header = GetElementDescription(value);
    }

    private readonly Dictionary<int, string> _processNameCache = new();

    private string? GetElementDescription(IVisualElement? element)
    {
        if (element is null) return LocaleResolver.Common_None;

        DynamicResourceKey key;
        var elementTypeKey = new DynamicResourceKey($"VisualElementType_{element.Type}");
        if (element.ProcessId != 0)
        {
            if (!_processNameCache.TryGetValue(element.ProcessId, out var processName))
            {
                try
                {
                    using var process = Process.GetProcessById(element.ProcessId);
                    processName = process.ProcessName;
                }
                catch
                {
                    processName = string.Empty;
                }
                _processNameCache[element.ProcessId] = processName;
            }

            key = processName.IsNullOrWhiteSpace() ?
                elementTypeKey :
                new FormattedDynamicResourceKey("{0} - {1}", new DirectResourceKey(processName), elementTypeKey);
        }
        else
        {
            key = elementTypeKey;
        }

        return key.ToString();
    }
}