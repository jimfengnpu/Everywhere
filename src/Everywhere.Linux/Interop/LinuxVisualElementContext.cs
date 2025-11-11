using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Everywhere.Interop;
using Microsoft.Extensions.Logging;
using ZLinq;
namespace Everywhere.Linux.Interop;

/// <summary>
/// Linux IVisualElementContext Impl:
/// Using ILinuxDisplayBackend for keyboard/mouse event and windows info.
/// Using AtspiService to access UI elements inside window.
/// </summary>
public partial class LinuxVisualElementContext: IVisualElementContext
{
    private readonly INativeHelper _nativeHelper;
    public readonly ILinuxDisplayBackend _backend;
    private readonly AtspiService _atspi;
    private readonly ILogger<LinuxVisualElementContext> _logger;
    public IVisualElement? KeyboardFocusedElement {
        get
        {
            try
            {
                return _atspi.ElementFocused();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KeyboardFocusedElement failed");
                return null;
            }
        }
    }

    public event IVisualElementContext.KeyboardFocusedElementChangedHandler? KeyboardFocusedElementChanged;

    public LinuxVisualElementContext(INativeHelper nativeHelper, ILinuxDisplayBackend backend, ILogger<LinuxVisualElementContext> logger)
    {
        _nativeHelper = nativeHelper;
        _backend = backend;
        backend.Context = this;
        _atspi = new AtspiService(this);
        _logger = logger;
    }
    

    public IVisualElement? ElementFromPoint(PixelPoint point, PickElementMode mode = PickElementMode.Element)
    {
        try
        {
            switch (mode)
            {
                case PickElementMode.Element:
                    var elem = _atspi.ElementFromPoint(point);
                    return elem ?? _backend.GetWindowElementAt(point);
                    
                case PickElementMode.Window:
                    return _backend.GetWindowElementAt(point);
                    
                case PickElementMode.Screen:
                    return _backend.GetScreenElement();
                    
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ElementFromPoint failed for point {point}, mode {mode}", point, mode);
            return null;
        }
    }

    public IVisualElement? ElementFromPointer(PickElementMode mode = PickElementMode.Element)
    {
        var point = _backend.GetPointer();
        return ElementFromPoint(point, mode);
    }

    public async Task<IVisualElement?> PickElementAsync(PickElementMode mode)
    {
        if (Application.Current is not { ApplicationLifetime: ClassicDesktopStyleApplicationLifetime desktopLifetime })
        {
            return null;
        }

        var windows = desktopLifetime.Windows.AsValueEnumerable().Where(w => w.IsVisible).ToList();
        foreach (var window in windows) window.Hide();
        var result = await ElementPicker.PickAsync(this, _backend, mode);
        foreach (var window in windows) window.IsVisible = true;
        return result;
    }

    
}
