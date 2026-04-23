// src/Orbital.App/Services/SingleInstanceGuard.cs
namespace Orbital.App.Services;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Global\Orbital.SingleInstance";

    private Mutex? mutex;
    private FileStream? pidFile;

    public bool TryAcquire()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                mutex = null;
                return false;
            }
            return true;
        }

        // macOS/Linux: pidfile with exclusive lock.
        var dir = Orbital.Core.Persistence.AppPaths.DataDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, ".orbital.pid");
        try
        {
            pidFile = new FileStream(
                path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            pidFile.SetLength(0);
            using var writer = new StreamWriter(pidFile, leaveOpen: true);
            writer.Write(Environment.ProcessId);
            writer.Flush();
            return true;
        }
        catch (IOException)
        {
            pidFile?.Dispose();
            pidFile = null;
            return false;
        }
    }

    public void Dispose()
    {
        mutex?.ReleaseMutex();
        mutex?.Dispose();
        pidFile?.Dispose();
        GC.SuppressFinalize(this);
    }
}
