// src/Orbital.App/Services/LinuxAutoStartService.cs
namespace Orbital.App.Services;

using System;
using System.IO;

public sealed class LinuxAutoStartService : IAutoStartService
{
    private static readonly string AutostartDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
    private const string Filename = "orbital.desktop";
    private static readonly string FilePath = Path.Combine(AutostartDir, Filename);

    private readonly string exePath = Environment.ProcessPath ?? "";

    public bool IsEnabled => File.Exists(FilePath);

    public void Enable()
    {
        Directory.CreateDirectory(AutostartDir);
        var content =
$@"[Desktop Entry]
Type=Application
Name=Orbital
Exec={exePath}
X-GNOME-Autostart-enabled=true
Hidden=false
Terminal=false
";
        File.WriteAllText(FilePath, content);
    }

    public void Disable()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
