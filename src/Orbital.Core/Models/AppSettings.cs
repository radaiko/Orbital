// src/Orbital.Core/Models/AppSettings.cs
namespace Orbital.Core.Models;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public HotkeyBinding QuickAddHotkey { get; init; } = HotkeyBinding.Default("T");
    public HotkeyBinding ToggleOverlayHotkey { get; init; } = HotkeyBinding.Default("L");
    public OverlayPosition OverlayPosition { get; init; } = OverlayPosition.TopRight;
    public bool OverlayAutoHideOnFocusLoss { get; init; } = true;
    public bool ShowCompleted { get; init; }
    public bool StartAtLogin { get; init; }
}
