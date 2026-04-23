// src/Orbital.Core/ViewModels/SettingsViewModel.cs
namespace Orbital.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Orbital.Core.Models;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings settings;

    public SettingsViewModel(AppSettings settings)
    {
        this.settings = settings;
        quickAddHotkey = Describe(settings.QuickAddHotkey);
        toggleOverlayHotkey = Describe(settings.ToggleOverlayHotkey);
        overlayPosition = settings.OverlayPosition;
        overlayAutoHideOnFocusLoss = settings.OverlayAutoHideOnFocusLoss;
        showCompleted = settings.ShowCompleted;
        startAtLogin = settings.StartAtLogin;
    }

    [ObservableProperty]
    private string quickAddHotkey;

    [ObservableProperty]
    private string toggleOverlayHotkey;

    [ObservableProperty]
    private OverlayPosition overlayPosition;

    [ObservableProperty]
    private bool overlayAutoHideOnFocusLoss;

    [ObservableProperty]
    private bool showCompleted;

    [ObservableProperty]
    private bool startAtLogin;

    public AppSettings Build()
    {
        return settings with
        {
            QuickAddHotkey = ParseOrKeep(QuickAddHotkey, settings.QuickAddHotkey),
            ToggleOverlayHotkey = ParseOrKeep(ToggleOverlayHotkey, settings.ToggleOverlayHotkey),
            OverlayPosition = OverlayPosition,
            OverlayAutoHideOnFocusLoss = OverlayAutoHideOnFocusLoss,
            ShowCompleted = ShowCompleted,
            StartAtLogin = StartAtLogin,
        };
    }

    private static string Describe(HotkeyBinding b)
    {
        var parts = new List<string>();
        if (b.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (b.Modifiers.HasFlag(HotkeyModifiers.Alt))     parts.Add("Alt");
        if (b.Modifiers.HasFlag(HotkeyModifiers.Shift))   parts.Add("Shift");
        if (b.Modifiers.HasFlag(HotkeyModifiers.Meta))    parts.Add("Meta");
        parts.Add(b.KeyName);
        return string.Join("+", parts);
    }

    private static HotkeyBinding ParseOrKeep(string text, HotkeyBinding fallback)
    {
        var tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return fallback;
        var mods = HotkeyModifiers.None;
        string key = "";
        foreach (var t in tokens)
        {
            switch (t.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= HotkeyModifiers.Control; break;
                case "alt" or "option":   mods |= HotkeyModifiers.Alt; break;
                case "shift":             mods |= HotkeyModifiers.Shift; break;
                case "meta" or "cmd" or "win": mods |= HotkeyModifiers.Meta; break;
                default: key = t.ToUpperInvariant(); break;
            }
        }
        if (mods == HotkeyModifiers.None || string.IsNullOrEmpty(key)) return fallback;
        return new HotkeyBinding(mods, key);
    }
}
