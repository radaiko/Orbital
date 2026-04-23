// src/Orbital.App/App.axaml.cs
namespace Orbital.App;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Orbital.App.Services;
using Orbital.App.Views;
using Orbital.Core.Persistence;
using Orbital.Core.ViewModels;

public partial class App : Application, IDisposable
{
    private TrayIconController? tray;
    private AppHost? host;
    private SharpHookGlobalHotkeyService? hotkeys;
    private QuickAddController? quickAdd;
    private OverlayController? overlay;
    private IDisposable? quickAddHotkeyReg;
    private IDisposable? overlayHotkeyReg;
    private readonly IAutoStartService autoStart = AutoStartServiceFactory.Create();

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

        // Sync StartAtLogin setting with the actual OS autostart state on first boot.
        if (host.Settings.StartAtLogin != autoStart.IsEnabled)
        {
            host.Settings = host.Settings with { StartAtLogin = autoStart.IsEnabled };
            await host.SaveSettingsAsync();
        }

        // Apply autostart changes whenever settings are saved.
        host.SettingsChanged += () =>
        {
            if (host.Settings.StartAtLogin && !autoStart.IsEnabled) autoStart.Enable();
            else if (!host.Settings.StartAtLogin && autoStart.IsEnabled) autoStart.Disable();
        };

        quickAdd = new QuickAddController(host);
        overlay = new OverlayController(host);

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

        RegisterHotkeys();
        host.SettingsChanged += RegisterHotkeys;

        tray = new TrayIconController();
        tray.QuickAddRequested += quickAdd.Toggle;
        tray.ToggleOverlayRequested += overlay.Toggle;
        tray.SettingsRequested += ShowSettings;
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

        overlay!.SettingsRequested += ShowSettings;
    }

    private void RegisterHotkeys()
    {
        quickAddHotkeyReg?.Dispose();
        overlayHotkeyReg?.Dispose();
        quickAddHotkeyReg = hotkeys!.Register(host!.Settings.QuickAddHotkey, () => quickAdd!.Toggle());
        overlayHotkeyReg  = hotkeys.Register(host.Settings.ToggleOverlayHotkey, () => overlay!.Toggle());
    }

    private void ShowSettings()
    {
        if (host is null) return;
        var vm = new SettingsViewModel(host.Settings);
        var w = new SettingsWindow { DataContext = vm };
        w.SaveRequested += async () =>
        {
            try
            {
                host.Settings = vm.Build();
                await host.SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                // async-void event handler: surface to log, never crash the app.
                Debug.WriteLine($"Failed to save settings: {ex}");
            }
            finally
            {
                w.Close();
            }
        };
        w.CancelRequested += () => w.Close();
        w.Show();
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
