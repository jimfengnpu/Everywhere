using Avalonia.Controls;
using Everywhere.Common;
using Everywhere.Configuration;
using ZLinq;
using Window = ShadUI.Window;

namespace Everywhere.AttachedProperties;

public static class SaveWindowPlacementAssist
{
    /// <summary>
    ///     Defines the attached property for the key used to save and restore the window's placement.
    /// </summary>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<Window, Window, string?>("Key");

    /// <summary>
    ///     Sets the key used to save and restore the window's placement.
    ///     If null, the window's placement will not be saved.
    /// </summary>
    public static void SetKey(Window obj, string? value) => obj.SetValue(KeyProperty, value);

    /// <summary>
    ///     Gets the key used to save and restore the window's placement.
    ///     If null, the window's placement will not be saved.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static string? GetKey(Window obj) => obj.GetValue(KeyProperty);

    private static readonly IKeyValueStorage KeyValueStorage = ServiceLocator.Resolve<IKeyValueStorage>();

    static SaveWindowPlacementAssist()
    {
        KeyProperty.Changed.AddClassHandler<Window>(HandleKeyPropertyChanged);
    }

    private static void HandleKeyPropertyChanged(Window sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is not string { Length: > 0 } key) return;

        // immediately try to restore window placement
        RestoreWindowPlacement(key, sender);

        // subscribe to window events
        sender.PositionChanged += (_, _) => SaveWindowPlacement(key, sender);
        sender.Resized += (_, _) => SaveWindowPlacement(key, sender);
    }

    private static void RestoreWindowPlacement(string key, Window window)
    {
        if (KeyValueStorage.Get<WindowPlacement?>($"TransientWindow.Placement.{key}") is not { } placement) return;

        if (window.Screens.All.Count == 0) return;

        var screenBounds = window.Screens.All.AsValueEnumerable()
            .Select(x => x.WorkingArea)
            .Aggregate((a, b) => a.Union(b));

        // Leave a safety margin to avoid window being too close to the edge or taskbar
        const int SafetyPadding = 20;

        var newX = Math.Clamp(placement.X,
            screenBounds.X + SafetyPadding,
            Math.Max(screenBounds.X + SafetyPadding, screenBounds.Right - placement.Width - SafetyPadding));

        var newY = Math.Clamp(placement.Y,
            screenBounds.Y + SafetyPadding,
            Math.Max(screenBounds.Y + SafetyPadding, screenBounds.Bottom - placement.Height - SafetyPadding));

        placement.X = newX;
        placement.Y = newY;

        if (placement.WindowState == WindowState.Normal)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = placement.Position;
            window.Width = placement.Width;
            window.Height = placement.Height;
            window.WindowState = WindowState.Normal;
        }
        else
        {
            window.WindowState = placement.WindowState;
        }
    }

    private static void SaveWindowPlacement(string key, Window window)
    {
        var placement = new WindowPlacement(
            window.Position,
            (int)window.Width,
            (int)window.Height,
            window.WindowState);
        KeyValueStorage.Set($"TransientWindow.Placement.{key}", placement);
    }
}