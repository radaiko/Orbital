// src/Orbital.App/Program.cs
namespace Orbital.App;

using Avalonia;
using Orbital.App.Services;
using Velopack;

internal sealed class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        // Velopack must run BEFORE any other startup logic. It handles the
        // --squirrel-install / --squirrel-uninstall / --squirrel-updated
        // subcommands Velopack injects and exits before we get here. For
        // normal launches it returns immediately.
        VelopackApp.Build().Run();

        using var guard = new SingleInstanceGuard();
        if (!guard.TryAcquire())
        {
            // Another instance is already running; exit silently.
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
