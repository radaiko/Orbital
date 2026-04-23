// src/Orbital.App/Services/OverlayController.cs
namespace Orbital.App.Services;

using System;
using System.ComponentModel;
using Avalonia;
using Microsoft.Extensions.Logging;
using Orbital.App.Views;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;

public sealed partial class OverlayController
{
    private readonly AppHost host;
    private readonly ILogger<OverlayController> log;
    private OverlayWindow? window;
    private OverlayViewModel? vm;

    public event Action? SettingsRequested;

    public OverlayController(AppHost host, ILogger<OverlayController> log)
    {
        this.host = host;
        this.log = log;
        host.SettingsChanged += ApplySettingsToLiveOverlay;
    }

    public void Toggle()
    {
        if (window is { IsVisible: true })
        {
            window.Hide();
            return;
        }
        Show();
    }

    private void Show()
    {
        EnsureWindow();
        PositionOnActiveScreen(window!);
        window!.Show();
        window.Activate();
    }

    private void EnsureWindow()
    {
        if (window is not null) return;
        vm = new OverlayViewModel(host.Todos) { ShowCompleted = host.Settings.ShowCompleted };
        vm.TodosMutated += () => host.ScheduleSaveTodos();
        vm.PropertyChanged += OnVmPropertyChanged;
        window = new OverlayWindow { DataContext = vm };
        window.Deactivated += OnWindowDeactivated;
        window.CloseRequested += () => window!.Hide();
        window.SettingsRequested += () => SettingsRequested?.Invoke();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (host.Settings.OverlayAutoHideOnFocusLoss)
            window?.Hide();
    }

    private async void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (vm is null) return;
        if (e.PropertyName != nameof(OverlayViewModel.ShowCompleted)) return;
        // Persist the in-overlay toggle. Guard against echo: only save when
        // Settings actually differ from the VM (otherwise SettingsChanged → VM
        // assignment would loop).
        if (host.Settings.ShowCompleted == vm.ShowCompleted) return;
        host.Settings = host.Settings with { ShowCompleted = vm.ShowCompleted };
        try
        {
            await host.SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            LogPersistShowCompletedFailed(log, ex);
        }
    }

    private void ApplySettingsToLiveOverlay()
    {
        if (vm is null) return;
        if (vm.ShowCompleted != host.Settings.ShowCompleted)
            vm.ShowCompleted = host.Settings.ShowCompleted;
        if (window is { IsVisible: true })
            PositionOnActiveScreen(window);
    }

    private void PositionOnActiveScreen(OverlayWindow w)
    {
        var screen = w.Screens.ScreenFromWindow(w) ?? w.Screens.Primary;
        if (screen is null) return;
        var wa = screen.WorkingArea;
        var scale = screen.Scaling;
        var physicalW = (int)(w.Width * scale);
        var physicalH = (int)(w.Height * scale);
        const int margin = 24;
        var (x, y) = host.Settings.OverlayPosition switch
        {
            OverlayPosition.TopRight    => (wa.Right - physicalW - margin,  wa.Y + margin),
            OverlayPosition.TopLeft     => (wa.X + margin,                  wa.Y + margin),
            OverlayPosition.BottomRight => (wa.Right - physicalW - margin,  wa.Bottom - physicalH - margin),
            OverlayPosition.BottomLeft  => (wa.X + margin,                  wa.Bottom - physicalH - margin),
            _ => (wa.Right - physicalW - margin, wa.Y + margin),
        };
        w.Position = new PixelPoint(x, y);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Persist ShowCompleted failed")]
    private static partial void LogPersistShowCompletedFailed(ILogger logger, Exception ex);
}
