// src/Orbital.Core/Models/HotkeyBinding.cs
namespace Orbital.Core.Models;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
    Meta = 8, // Cmd on macOS, Windows key on Windows
}

public sealed record HotkeyBinding(HotkeyModifiers Modifiers, string KeyName)
{
    public static HotkeyBinding Default(string keyName) =>
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, keyName);
}
