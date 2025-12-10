using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Everywhere.Common;
using Everywhere.Configuration;
using ShadUI.Themes;

namespace Everywhere.Views;

public partial class MainView : ReactiveUserControl<MainViewModel>
{
    private readonly Settings _settings;
    private Window? _mainWindow;

    public MainView(Settings settings)
    {
        _settings = settings;

        InitializeComponent();
    }

    private void HandleSettingsCommonPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommonSettings.Theme)) return;
        Dispatcher.UIThread.InvokeOnDemand(ApplyTheme);
    }

    private void RestoreWindowBounds()
    {
        if (_mainWindow is null) return;
        if (_settings.Internal.MainWindowPlacement is not { } placement) return;

        _mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        _mainWindow.Position = placement.Position;
        _mainWindow.Width = placement.Width;
        _mainWindow.Height = placement.Height;
        _mainWindow.WindowState = placement.WindowState;
    }

    private void SaveMainWindowBounds()
    {
        if (_mainWindow is null) return;

        _settings.Internal.MainWindowPlacement = new WindowPlacement(
            _mainWindow.Position.X,
            _mainWindow.Position.Y,
            (int)_mainWindow.Width,
            (int)_mainWindow.Height,
            _mainWindow.WindowState);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _settings.Common.PropertyChanged += HandleSettingsCommonPropertyChanged;
        _mainWindow = TopLevel.GetTopLevel(this) as Window;
        RestoreWindowBounds();
        _mainWindow?.Closing += HandleMainWindowClosing;
        ApplyTheme();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        _settings.Common.PropertyChanged -= HandleSettingsCommonPropertyChanged;
        _mainWindow?.Closing -= HandleMainWindowClosing;
        _mainWindow = null;
    }

    private void HandleMainWindowClosing(object? sender, WindowClosingEventArgs e)
        => SaveMainWindowBounds();

    private void ApplyTheme()
    {
        _mainWindow?.RequestedThemeVariant = _settings.Common.Theme switch
        {
            "Dark" => ThemeVariants.Dark,
            "Light" => ThemeVariants.Light,
            _ => ThemeVariants.Default
        };
    }
}