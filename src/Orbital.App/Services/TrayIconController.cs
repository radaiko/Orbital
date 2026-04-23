// src/Orbital.App/Services/TrayIconController.cs
namespace Orbital.App.Services;

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

public sealed class TrayIconController : IDisposable
{
    private TrayIcon? trayIcon;

    public event Action? QuickAddRequested;
    public event Action? ToggleOverlayRequested;
    public event Action? SettingsRequested;
    public event Action? OpenDataFolderRequested;
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
