// src/Orbital.App/App.axaml.cs
namespace Orbital.App;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Orbital.App.Services;
using Orbital.Core.Persistence;

public partial class App : Application, IDisposable
{
    private TrayIconController? tray;
    private AppHost? host;
    private SharpHookGlobalHotkeyService? hotkeys;

    public AppHost? Host => host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            AppPaths.EnsureDataDirectoryExists();

            // Kick off async initialization without making the override async void.
            _ = InitializeAsync(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        host = new AppHost();
        await host.LoadAsync();

        hotkeys = new SharpHookGlobalHotkeyService();

        // StartAsync runs the low-level hook on a background thread. On macOS
        // the OS may deny Accessibility permission; handle both synchronous and
        // asynchronous failures so an unobserved task doesn't crash the app.
        try
        {
            var startTask = hotkeys.StartAsync();
            _ = startTask.ContinueWith(
                t => Debug.WriteLine($"Global hotkey hook failed: {t.Exception}"),
                TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start global hotkey hook: {ex}");
        }

        hotkeys.Register(host.Settings.QuickAddHotkey, () =>
        {
            // Temporary: confirmed by Debug output; real window wired in Task 16.
            Debug.WriteLine("Quick-add hotkey pressed");
        });
        hotkeys.Register(host.Settings.ToggleOverlayHotkey, () =>
        {
            Debug.WriteLine("Overlay hotkey pressed");
        });

        tray = new TrayIconController();
        tray.QuitRequested += async () =>
        {
            await host!.FlushAsync();
            if (hotkeys is not null) await hotkeys.DisposeAsync();
            host.Dispose();
            tray?.Dispose();
            desktop.Shutdown();
        };
        tray.OpenDataFolderRequested += OpenDataFolder;
        tray.Show();
    }

    private static void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.DataDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open data folder: {ex}");
        }
    }

    public void Dispose()
    {
        tray?.Dispose();
        // Best-effort synchronous disposal path for abnormal exits (SIGTERM etc.).
        // The normal QuitRequested path disposes these with async flush first.
        if (hotkeys is not null)
        {
            try { hotkeys.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception ex) { Debug.WriteLine($"Hotkey disposal failed: {ex}"); }
        }
        host?.Dispose();
        GC.SuppressFinalize(this);
    }
}
