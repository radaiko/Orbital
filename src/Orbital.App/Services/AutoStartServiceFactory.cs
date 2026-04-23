// src/Orbital.App/Services/AutoStartServiceFactory.cs
namespace Orbital.App.Services;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

public static class AutoStartServiceFactory
{
    public static IAutoStartService Create(ILoggerFactory? loggers = null)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsAutoStartService();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacAutoStartService(loggers?.CreateLogger<MacAutoStartService>());
        return new LinuxAutoStartService();
    }
}
