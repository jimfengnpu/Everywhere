using Avalonia.Input;

namespace Everywhere.Mac.Interop;

/// <summary>
/// Provides mapping from macOS specific key codes and flags to Avalonia key enums.
/// </summary>
public static class KeyMapping
{
    /// <summary>
    /// Converts CGEventFlags to Avalonia's KeyModifiers.
    /// </summary>
    /// <param name="flags"></param>
    /// <returns></returns>
    public static KeyModifiers ToAvaloniaKeyModifiers(this CGEventFlags flags)
    {
        var modifiers = KeyModifiers.None;
        if (flags.HasFlag(CGEventFlags.Shift))
            modifiers |= KeyModifiers.Shift;
        if (flags.HasFlag(CGEventFlags.Control))
            modifiers |= KeyModifiers.Control;
        if (flags.HasFlag(CGEventFlags.Alternate))
            modifiers |= KeyModifiers.Alt;
        if (flags.HasFlag(CGEventFlags.Command))
            modifiers |= KeyModifiers.Meta;
        return modifiers;
    }

    /// <summary>
    /// Converts NSEventModifierMask to Avalonia's KeyModifiers.
    /// </summary>
    public static KeyModifiers ToAvaloniaKeyModifiers(this NSEventModifierMask flags)
    {
        var modifiers = KeyModifiers.None;
        if (flags.HasFlag(NSEventModifierMask.ShiftKeyMask))
            modifiers |= KeyModifiers.Shift;
        if (flags.HasFlag(NSEventModifierMask.ControlKeyMask))
            modifiers |= KeyModifiers.Control;
        if (flags.HasFlag(NSEventModifierMask.AlternateKeyMask))
            modifiers |= KeyModifiers.Alt;
        if (flags.HasFlag(NSEventModifierMask.CommandKeyMask))
            modifiers |= KeyModifiers.Meta;
        return modifiers;
    }

    /// <summary>
    /// Converts a macOS virtual key code to an Avalonia Key.
    /// </summary>
    public static Key ToAvaloniaKey(this ushort macKeyCode)
    {
        return MacToAvaloniaMap.TryGetValue(macKeyCode, out var key) ? key : Key.None;
    }

    // This is a partial mapping. You would need to extend it for full coverage.
    // Key codes can be found in macOS's <Carbon/Events.h> or online resources.
    private static readonly Dictionary<ushort, Key> MacToAvaloniaMap = new()
    {
        // Letters
        { 0x00, Key.A }, { 0x0B, Key.B }, { 0x08, Key.C }, { 0x02, Key.D }, { 0x0E, Key.E },
        { 0x03, Key.F }, { 0x05, Key.G }, { 0x04, Key.H }, { 0x22, Key.I }, { 0x26, Key.J },
        { 0x28, Key.K }, { 0x25, Key.L }, { 0x2E, Key.M }, { 0x2D, Key.N }, { 0x1F, Key.O },
        { 0x23, Key.P }, { 0x0C, Key.Q }, { 0x0F, Key.R }, { 0x01, Key.S }, { 0x11, Key.T },
        { 0x20, Key.U }, { 0x09, Key.V }, { 0x0D, Key.W }, { 0x07, Key.X }, { 0x10, Key.Y },
        { 0x06, Key.Z },

        // Numbers
        { 0x1D, Key.D0 }, { 0x12, Key.D1 }, { 0x13, Key.D2 }, { 0x14, Key.D3 }, { 0x15, Key.D4 },
        { 0x17, Key.D5 }, { 0x16, Key.D6 }, { 0x1A, Key.D7 }, { 0x1C, Key.D8 }, { 0x19, Key.D9 },

        // Numpad
        { 0x52, Key.NumPad0 }, { 0x53, Key.NumPad1 }, { 0x54, Key.NumPad2 }, { 0x55, Key.NumPad3 },
        { 0x56, Key.NumPad4 }, { 0x57, Key.NumPad5 }, { 0x58, Key.NumPad6 }, { 0x59, Key.NumPad7 },
        { 0x5B, Key.NumPad8 }, { 0x5C, Key.NumPad9 },
        { 0x45, Key.Add }, { 0x4E, Key.Subtract }, { 0x43, Key.Multiply }, { 0x4B, Key.Divide },
        { 0x41, Key.Decimal }, { 0x4C, Key.Enter },

        // Function keys
        { 0x7A, Key.F1 }, { 0x78, Key.F2 }, { 0x63, Key.F3 }, { 0x76, Key.F4 }, { 0x60, Key.F5 },
        { 0x61, Key.F6 }, { 0x62, Key.F7 }, { 0x64, Key.F8 }, { 0x65, Key.F9 }, { 0x6D, Key.F10 },
        { 0x67, Key.F11 }, { 0x6F, Key.F12 },

        // Special keys
        { 0x35, Key.Escape }, { 0x31, Key.Space }, { 0x24, Key.Enter }, { 0x30, Key.Tab },
        { 0x33, Key.Back }, { 0x75, Key.Delete },
        { 0x7E, Key.Up }, { 0x7D, Key.Down }, { 0x7B, Key.Left }, { 0x7C, Key.Right },
        { 0x73, Key.Home }, { 0x77, Key.End }, { 0x74, Key.PageUp }, { 0x79, Key.PageDown },
    };
}