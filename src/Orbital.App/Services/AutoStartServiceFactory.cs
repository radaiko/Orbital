// src/Orbital.App/Services/AutoStartServiceFactory.cs
namespace Orbital.App.Services;

using System.Runtime.InteropServices;

public static class AutoStartServiceFactory
{
    public static IAutoStartService Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsAutoStartService();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     return new MacAutoStartService();
        return new LinuxAutoStartService();
    }
}
