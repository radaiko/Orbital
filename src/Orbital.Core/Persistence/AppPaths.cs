// src/Orbital.Core/Persistence/AppPaths.cs
namespace Orbital.Core.Persistence;

using System.Runtime.InteropServices;

public static class AppPaths
{
    private const string AppDirName = "Orbital";

    public static string DataDirectory { get; } = ResolveDataDirectory();
    public static string TodosFile { get; } = Path.Combine(DataDirectory, "todos.json");
    public static string SettingsFile { get; } = Path.Combine(DataDirectory, "settings.json");
    public static string TodosBackupFile { get; } = Path.Combine(DataDirectory, "todos.backup.json");

    private static string ResolveDataDirectory()
    {
        string baseDir;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // %APPDATA% = C:\Users\<u>\AppData\Roaming
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            baseDir = Path.Combine(home, "Library", "Application Support");
        }
        else
        {
            // Linux / BSD / other: XDG_DATA_HOME or ~/.local/share
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            baseDir = !string.IsNullOrEmpty(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
        return Path.Combine(baseDir, AppDirName);
    }

    public static void EnsureDataDirectoryExists()
    {
        Directory.CreateDirectory(DataDirectory);
    }
}
