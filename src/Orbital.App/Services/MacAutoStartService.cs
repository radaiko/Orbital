// src/Orbital.App/Services/MacAutoStartService.cs
namespace Orbital.App.Services;

using System;
using System.Diagnostics;
using System.IO;
using System.Security;

public sealed class MacAutoStartService : IAutoStartService
{
    private static readonly string LaunchAgentsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
    private const string Label = "dev.orbital.app";
    private static readonly string PlistPath = Path.Combine(LaunchAgentsDir, Label + ".plist");

    private readonly string exePath = Environment.ProcessPath ?? "";

    public bool IsEnabled => File.Exists(PlistPath);

    public void Enable()
    {
        Directory.CreateDirectory(LaunchAgentsDir);
        // XML-escape the path — Application folders may contain &, <, > etc.
        var escapedExe = SecurityElement.Escape(exePath);
        var plist =
$@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key><string>{Label}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{escapedExe}</string>
    </array>
    <key>RunAtLoad</key><true/>
    <key>LimitLoadToSessionType</key><string>Aqua</string>
</dict>
</plist>";
        File.WriteAllText(PlistPath, plist);
        Run("launchctl", $"load -w \"{PlistPath}\"");
    }

    public void Disable()
    {
        if (File.Exists(PlistPath))
            Run("launchctl", $"unload -w \"{PlistPath}\"");
        File.Delete(PlistPath);
    }

    private static void Run(string fileName, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(fileName, args) { UseShellExecute = false });
            p?.WaitForExit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoStart] launchctl failed: {ex.Message}");
        }
    }
}
