// src/Orbital.App/App.axaml.cs
namespace Orbital.App;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Orbital.App.Logging;
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
    private IAutoStartService? autoStart;
    private ILoggerFactory? loggerFactory;
    private FileLoggerProvider? fileProvider;
    private ILogger<App>? log;
    private DialogService? dialogs;
#pragma warning disable CA1859 // interface field is intentional — allows future test injection
    private IUpdateService? updates;
#pragma warning restore CA1859

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
        (loggerFactory, fileProvider) = LoggingSetup.Create();
        log = loggerFactory.CreateLogger<App>();
        var version = typeof(App).Assembly.GetName().Version?.ToString(3);
        LogStarting(log, version);
        dialogs = new DialogService();

        updates = new VelopackUpdateService(loggerFactory!.CreateLogger<VelopackUpdateService>());
        updates.UpdateAvailable += OnUpdateAvailable;
        updates.UpToDate += OnUpToDate;
        updates.UpdateFailed += OnUpdateFailed;

        autoStart = AutoStartServiceFactory.Create(loggerFactory);

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
        overlay = new OverlayController(host, loggerFactory.CreateLogger<OverlayController>());

        hotkeys = new SharpHookGlobalHotkeyService(loggerFactory.CreateLogger<SharpHookGlobalHotkeyService>());

        // StartAsync runs the low-level hook on a background thread. On macOS
        // the OS may deny Accessibility permission; handle both synchronous and
        // asynchronous failures so an unobserved task doesn't crash the app.
        try
        {
            var startTask = hotkeys.StartAsync();
            _ = startTask.ContinueWith(t =>
            {
                LogHotkeyHookFailedAsync(log!, t.Exception);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Dispatcher.UIThread.Post(() => tray?.SetAccessibilityDenied(true));
                    Dispatcher.UIThread.Post(() => _ = dialogs!.ShowErrorAsync(
                        "Accessibility permission required",
                        "Orbital needs Accessibility access to listen for your hotkeys. Open System Settings → Privacy & Security → Accessibility, toggle Orbital on, then quit and relaunch the app."));
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            LogHotkeyHookFailed(log!, ex);
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
            updates?.Dispose();
            updates = null; // prevent App.Dispose() from re-disposing
            host.Dispose();
            tray?.Dispose();
            desktop.Shutdown();
        };
        tray.OpenDataFolderRequested += OpenDataFolder;
        tray.AboutRequested += ShowAbout;
        tray.Show();

        // Wire update service to tray menu interactions.
        tray.CheckForUpdatesRequested += async () => await updates!.CheckAsync();
        tray.UpdateNowRequested += async () => await updates!.ApplyAndRestartAsync();
        if (updates.IsSupported)
            tray.SetUpdateState(TrayIconController.UpdateMenuState.Idle);

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
                // async-void event handler: surface to log + dialog, never crash the app.
                LogSaveSettingsFailed(log!, ex);
                await dialogs!.ShowErrorAsync(
                    "Couldn't save settings",
                    "Something went wrong writing settings.json. Your changes were not saved. See the log file for details.");
            }
            finally
            {
                w.Close();
            }
        };
        w.CancelRequested += () => w.Close();
        w.Show();
    }

    private void ShowAbout()
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "dev";
        var vm = new AboutViewModel(version);
        var w = new AboutWindow { DataContext = vm };
        // Trigger a real check; the sync Func<bool> return is indicative —
        // the user sees the actual result reflected in the tray menu shortly after.
        w.CheckUpdatesRequested = () =>
        {
            if (updates is null || !updates.IsSupported) return false;
            _ = updates.CheckAsync();
            return updates.IsUpdateAvailable;
        };
        w.Show();
    }

    private void OpenDataFolder()
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
            if (log is not null) LogOpenDataFolderFailed(log, ex);
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
            catch (Exception ex)
            {
                if (log is not null) LogHotkeyDisposalFailed(log, ex);
            }
        }
        updates?.Dispose();
        host?.Dispose();
        // LoggerFactory owns the FileLoggerProvider (via AddProvider) and
        // disposes it on its own Dispose. Do not double-dispose.
        loggerFactory?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnUpdateAvailable()
    {
        Dispatcher.UIThread.Post(() =>
            tray?.SetUpdateState(TrayIconController.UpdateMenuState.Available, updates?.AvailableVersion));
    }

    private void OnUpToDate()
    {
        Dispatcher.UIThread.Post(() =>
        {
            tray?.SetUpdateState(TrayIconController.UpdateMenuState.UpToDate);
            // Revert to Idle after 3 s so the user can manually check again.
            var timer = new System.Timers.Timer(3000) { AutoReset = false };
            timer.Elapsed += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                    tray?.SetUpdateState(TrayIconController.UpdateMenuState.Idle));
                timer.Dispose();
            };
            timer.Start();
        });
    }

    private async void OnUpdateFailed(Exception ex)
    {
        LogUpdateFailed(log!, ex);
        await (dialogs?.ShowErrorAsync(
            "Update failed",
            $"Could not update Orbital: {ex.Message}\n\nSee the log file for details.") ?? Task.CompletedTask);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Orbital starting — version {Version}")]
    private static partial void LogStarting(ILogger logger, string? version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start global hotkey hook")]
    private static partial void LogHotkeyHookFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Global hotkey hook failed asynchronously")]
    private static partial void LogHotkeyHookFailedAsync(ILogger logger, Exception? ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save settings")]
    private static partial void LogSaveSettingsFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to open data folder")]
    private static partial void LogOpenDataFolderFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hotkey disposal failed")]
    private static partial void LogHotkeyDisposalFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Update failed")]
    private static partial void LogUpdateFailed(ILogger logger, Exception ex);
}
