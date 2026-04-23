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
    private TrayIcon? trayIcon;
    private bool accessibilityDenied;

    public event Action? QuickAddRequested;
    public event Action? ToggleOverlayRequested;
    public event Action? SettingsRequested;
    public event Action? OpenDataFolderRequested;
    public event Action? AboutRequested;
    public event Action? QuitRequested;

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

    private void RebuildMenuWithAccessibility()
    {
        if (trayIcon is null) return;
        var menu = BuildMenu();
        if (accessibilityDenied)
        {
            var item = new NativeMenuItem("⚠ Grant Accessibility…");
            item.Click += (_, _) => OpenAccessibilityPane();
            menu.Items.Insert(0, item);
            menu.Items.Insert(1, new NativeMenuItemSeparator());
        }
        trayIcon.Menu = menu;
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
