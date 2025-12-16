using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Everywhere.Interop;

namespace Everywhere.Views;

/// <summary>
/// Base window class for visual element picking.
/// </summary>
public abstract class VisualElementPickerWindow : Window
{
    protected VisualElementPickerWindow()
    {
        Topmost = true;
        CanResize = false;
        CanMaximize = false;
        CanMinimize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
    }
}

/// <summary>
/// Transparent window used for visual element picking.
/// Provides methods to set placement based on screen bounds.
/// </summary>
public class VisualElementPickerTransparentWindow : VisualElementPickerWindow
{
    public VisualElementPickerTransparentWindow()
    {
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Cross);
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        SystemDecorations = SystemDecorations.None;
    }

    /// <summary>
    /// Sets the window placement based on the specified screen bounds.
    /// </summary>
    /// <param name="screenBounds"></param>
    /// <param name="scale"></param>
    public void SetPlacement(PixelRect screenBounds, out double scale)
    {
        Position = screenBounds.Position;
        scale = DesktopScaling; // we must set Position first to get the correct scaling factor
        Width = screenBounds.Width / scale;
        Height = screenBounds.Height / scale;
    }
}

/// <summary>
/// Mask window that displays the overlay during element picking.
/// </summary>
public sealed class VisualElementPickerMaskWindow : VisualElementPickerTransparentWindow
{
    private readonly Border _maskBorder;
    private readonly Border _elementBoundsBorder;
    private readonly PixelRect _screenBounds;
    private readonly double _scale;

    public VisualElementPickerMaskWindow(PixelRect screenBounds)
    {
        Content = new Panel
        {
            IsHitTestVisible = false,
            Children =
            {
                (_maskBorder = new Border
                {
                    Background = Brushes.Black,
                    Opacity = 0.4
                }),
                (_elementBoundsBorder = new Border
                {
                    BorderThickness = new Thickness(2),
                    BorderBrush = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                })
            }
        };

        _screenBounds = screenBounds;
        SetPlacement(screenBounds, out _scale);
    }

    public void SetMask(PixelRect rect)
    {
        var maskRect = rect.Translate(-(PixelVector)_screenBounds.Position).ToRect(_scale);
        _maskBorder.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(Bounds), new RectangleGeometry(maskRect));
        _elementBoundsBorder.Margin = new Thickness(maskRect.X, maskRect.Y, 0, 0);
        _elementBoundsBorder.Width = maskRect.Width;
        _elementBoundsBorder.Height = maskRect.Height;
    }
}

public sealed class VisualElementPickerToolTipWindow : VisualElementPickerWindow
{
    public VisualElementPickerToolTip ToolTip { get; }

    public VisualElementPickerToolTipWindow(ElementPickMode elementPickMode)
    {
        Content = ToolTip = new VisualElementPickerToolTip
        {
            Mode = elementPickMode
        };
        SizeToContent = SizeToContent.WidthAndHeight;
        SystemDecorations = SystemDecorations.BorderOnly;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaToDecorationsHint = true;
    }
}