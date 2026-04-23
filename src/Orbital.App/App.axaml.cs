// src/Orbital.App/App.axaml.cs
namespace Orbital.App;

using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Orbital.App.Services;
using Orbital.Core.Persistence;

public partial class App : Application, IDisposable
{
    private TrayIconController? tray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            tray = new TrayIconController();
            tray.QuitRequested += () =>
            {
                tray?.Dispose();
                desktop.Shutdown();
            };
            tray.OpenDataFolderRequested += OpenDataFolder;
            tray.Show();

            AppPaths.EnsureDataDirectoryExists();
        }
        base.OnFrameworkInitializationCompleted();
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
            System.Diagnostics.Debug.WriteLine($"Failed to open data folder: {ex}");
        }
    }

    public void Dispose()
    {
        tray?.Dispose();
        GC.SuppressFinalize(this);
    }
}
