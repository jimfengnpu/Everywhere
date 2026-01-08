using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Everywhere.Interop;

namespace Everywhere.Views;

/// <summary>
/// Base window class for screen selection windows.
/// </summary>
public abstract class ScreenSelectionWindow : Window
{
    protected ScreenSelectionWindow()
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
/// Transparent window used for screen selection.
/// Provides methods to set placement based on screen bounds.
/// </summary>
public class ScreenSelectionTransparentWindow : ScreenSelectionWindow
{
    public ScreenSelectionTransparentWindow()
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
/// Mask window that displays the overlay during screen selection.
/// </summary>
public sealed class ScreenSelectionMaskWindow : ScreenSelectionTransparentWindow
{
    private readonly Border _maskBorder;
    private readonly Border _elementBoundsBorder;
    private readonly Image _imageControl;
    private readonly PixelRect _screenBounds;
    private readonly double _scale;

    public ScreenSelectionMaskWindow(PixelRect screenBounds)
    {
        Content = new Panel
        {
            IsHitTestVisible = false,
            Children =
            {
                (_imageControl = new Image
                {
                    Stretch = Stretch.UniformToFill
                }),
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

    public void SetImage(Bitmap? bitmap)
    {
        _imageControl.Source = bitmap;
        // If we have a background image, the "mask" effect logic changes.
        // The mask border (background black, opacity 0.4) is overlaying the image.
        // We want the SELECTED area to be clear (showing the image directly) and the UNSELECTED area to be darkened.
        // The 'SetMask' function uses 'Clip' to Exclude the selected rect from the mask border.
        // This is exactly what we want: Exclude rect -> that rect part of black border is gone -> underlying Image is visible. 
        // So no changes needed in SetMask logic if we assume standard freeze frame behavior.
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

public sealed class ScreenSelectionToolTipWindow : ScreenSelectionWindow
{
    public ScreenSelectionToolTip ToolTip { get; }

    public ScreenSelectionToolTipWindow(IEnumerable<ScreenSelectionMode> allowedModes, ScreenSelectionMode mode)
    {
        Content = ToolTip = new ScreenSelectionToolTip(allowedModes)
        {
            Mode = mode
        };
        SizeToContent = SizeToContent.WidthAndHeight;
        SystemDecorations = SystemDecorations.BorderOnly;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaToDecorationsHint = true;
    }
}