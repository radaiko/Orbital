// src/Orbital.App/Services/TrayIconController.cs
namespace Orbital.App.Services;

using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

public sealed class TrayIconController : IDisposable
{
    public enum UpdateMenuState { None, Idle, Available, UpToDate }

    private TrayIcon? trayIcon;
    private bool accessibilityDenied;
    private UpdateMenuState updateState = UpdateMenuState.None;
    private string? availableUpdateVersion;

    public event Action? QuickAddRequested;
    public event Action? ToggleOverlayRequested;
    public event Action? SettingsRequested;
    public event Action? OpenDataFolderRequested;
    public event Action? AboutRequested;
    public event Action? QuitRequested;
    public event Action? CheckForUpdatesRequested;
    public event Action? UpdateNowRequested;

    public void Show()
    {
        if (trayIcon is not null) return;

        trayIcon = new TrayIcon
        {
            ToolTipText = "Orbital",
            Icon = LoadIcon(),
            IsVisible = true,
        };
        trayIcon.Menu = BuildMenu();

        TrayIcon.SetIcons(Application.Current!, new TrayIcons { trayIcon });
    }

    public void SetAccessibilityDenied(bool denied)
    {
        if (accessibilityDenied == denied || trayIcon is null) return;
        accessibilityDenied = denied;
        RebuildMenuWithAccessibility();
    }

    public void SetUpdateState(UpdateMenuState state, string? version = null)
    {
        updateState = state;
        availableUpdateVersion = version;
        if (trayIcon is null) return;
        RebuildMenuWithAccessibility();
    }

    private void RebuildMenuWithAccessibility()
    {
        if (trayIcon is null) return;
        var menu = BuildMenu();

        // Accessibility banner at the very top
        if (accessibilityDenied)
        {
            var item = new NativeMenuItem("⚠ Grant Accessibility…");
            item.Click += (_, _) => OpenAccessibilityPane();
            menu.Items.Insert(0, item);
            menu.Items.Insert(1, new NativeMenuItemSeparator());
        }

        // Update item just above the "About Orbital…" / Quit items
        InsertUpdateMenuItem(menu);

        trayIcon.Menu = menu;
    }

    private void InsertUpdateMenuItem(NativeMenu menu)
    {
        if (updateState is UpdateMenuState.None) return;

        var item = new NativeMenuItem();
        switch (updateState)
        {
            case UpdateMenuState.Idle:
                item.Header = "Check for updates…";
                item.Click += (_, _) => CheckForUpdatesRequested?.Invoke();
                break;
            case UpdateMenuState.Available:
                item.Header = $"⬆ Update to v{availableUpdateVersion}";
                item.Click += (_, _) => UpdateNowRequested?.Invoke();
                break;
            case UpdateMenuState.UpToDate:
                item.Header = "✓ Up to date";
                item.IsEnabled = false;
                break;
        }

        // Insert just before the last two items (About + Quit), i.e. at count-2.
        var pos = Math.Max(0, menu.Items.Count - 2);
        menu.Items.Insert(pos, item);
        menu.Items.Insert(pos + 1, new NativeMenuItemSeparator());
    }

    private static void OpenAccessibilityPane()
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                "open",
                "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")
            {
                UseShellExecute = false,
            });
        }
        catch { /* best-effort */ }
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        var quickAdd = new NativeMenuItem("Quick add");
        quickAdd.Click += (_, _) => QuickAddRequested?.Invoke();
        menu.Add(quickAdd);

        var overlay = new NativeMenuItem("Show overlay");
        overlay.Click += (_, _) => ToggleOverlayRequested?.Invoke();
        menu.Add(overlay);

        menu.Add(new NativeMenuItemSeparator());

        var settings = new NativeMenuItem("Settings…");
        settings.Click += (_, _) => SettingsRequested?.Invoke();
        menu.Add(settings);

        var openFolder = new NativeMenuItem("Open data folder");
        openFolder.Click += (_, _) => OpenDataFolderRequested?.Invoke();
        menu.Add(openFolder);

        menu.Add(new NativeMenuItemSeparator());

        var about = new NativeMenuItem("About Orbital…");
        about.Click += (_, _) => AboutRequested?.Invoke();
        menu.Add(about);

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => QuitRequested?.Invoke();
        menu.Add(quit);

        return menu;
    }

    private static WindowIcon LoadIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Orbital.App/Assets/tray-icon.png"));
        return new WindowIcon(new Bitmap(stream));
    }

    public void Dispose()
    {
        if (trayIcon is not null)
        {
            trayIcon.IsVisible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }
    }
}
