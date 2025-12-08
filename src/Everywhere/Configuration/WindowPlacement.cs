using System.Text.Json.Serialization;
using Avalonia.Controls;

namespace Everywhere.Configuration;

/// <summary>
/// Represents the placement of a window (position, size and state).
/// </summary>
[Serializable]
public struct WindowPlacement(int x, int y, int width, int height, WindowState windowState) : IEquatable<WindowPlacement>
{
    public int X { get; set; } = x;
    public int Y { get; set; } = y;
    public int Width { get; set; } = width;
    public int Height { get; set; } = height;
    public WindowState WindowState { get; set; } = windowState;
    
    [JsonIgnore]
    public PixelPoint Position => new(X, Y);

    public bool Equals(WindowPlacement other) =>
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height && WindowState == other.WindowState;

    public override bool Equals(object? obj) => obj is WindowPlacement other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height, (int)WindowState);

    public static bool operator ==(WindowPlacement left, WindowPlacement right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(WindowPlacement left, WindowPlacement right)
    {
        return !(left == right);
    }
}