// src/Orbital.App/Program.cs
namespace Orbital.App;

using Avalonia;
using Orbital.App.Services;

internal sealed class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
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
