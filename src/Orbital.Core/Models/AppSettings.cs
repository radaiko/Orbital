// src/Orbital.Core/Models/AppSettings.cs
namespace Orbital.Core.Models;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public HotkeyBinding QuickAddHotkey { get; set; } = HotkeyBinding.Default("T");
    public HotkeyBinding ToggleOverlayHotkey { get; set; } = HotkeyBinding.Default("L");
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopRight;
    public bool OverlayAutoHideOnFocusLoss { get; set; } = true;
    public bool ShowCompleted { get; set; }
    public bool StartAtLogin { get; set; }
}
