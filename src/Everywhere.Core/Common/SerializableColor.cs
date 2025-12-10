using Avalonia.Media;

namespace Everywhere.Common;

[Serializable]
public struct SerializableColor : IEquatable<SerializableColor>
{
    public byte A { get; set; }
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public static implicit operator SerializableColor(Color color) => new()
    {
        A = color.A,
        R = color.R,
        G = color.G,
        B = color.B
    };

    public static implicit operator Color(SerializableColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    public bool Equals(SerializableColor other) => A == other.A && R == other.R && G == other.G && B == other.B;

    public override bool Equals(object? obj) => obj is SerializableColor other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(A, R, G, B);

    public static bool operator ==(SerializableColor left, SerializableColor right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SerializableColor left, SerializableColor right)
    {
        return !(left == right);
    }
}