// src/Orbital.App/Services/OverlayController.cs
namespace Orbital.App.Services;

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Orbital.App.Views;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;

public sealed class OverlayController
{
    private readonly AppHost host;
    private OverlayWindow? window;
    private OverlayViewModel? vm;

    public event Action? SettingsRequested;

    public OverlayController(AppHost host) { this.host = host; }

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
        window = new OverlayWindow { DataContext = vm };
        window.CloseRequested += () => Dispatcher.UIThread.Post(() => window.Hide());
        window.SettingsRequested += () => SettingsRequested?.Invoke();
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
}
