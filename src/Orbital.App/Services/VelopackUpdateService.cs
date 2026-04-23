// src/Orbital.App/Services/VelopackUpdateService.cs
namespace Orbital.App.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

public sealed partial class VelopackUpdateService : IUpdateService
{
    private readonly ILogger<VelopackUpdateService> log;
    private readonly UpdateManager? manager;
    private readonly Timer? periodic;
    // volatile: written on the timer's ThreadPool callback, read on the UI
    // thread via IsUpdateAvailable / AvailableVersion. On ARM (Apple Silicon)
    // the weak memory model requires a barrier for safe cross-thread visibility.
    private volatile UpdateInfo? pending;

    public bool IsSupported { get; }
    public bool IsUpdateAvailable => pending is not null;
    public string? AvailableVersion => pending?.TargetFullRelease?.Version?.ToString();

    public event Action? UpdateAvailable;
    public event Action? UpToDate;
    public event Action<Exception>? UpdateFailed;

    public VelopackUpdateService(ILogger<VelopackUpdateService> log)
    {
        this.log = log;
        try
        {
            manager = new UpdateManager(new GithubSource(
                "https://github.com/radaiko/Orbital",
                accessToken: null,
                prerelease: false));
            IsSupported = manager.IsInstalled;
        }
        catch (Exception ex)
        {
            LogManagerUnavailable(log, ex);
            IsSupported = false;
        }

        if (IsSupported)
        {
            // First check 10s after startup, then every 24h.
            periodic = new Timer(_ => _ = CheckAsync(), null,
                TimeSpan.FromSeconds(10), TimeSpan.FromHours(24));
        }
    }

    public async Task CheckAsync(CancellationToken ct = default)
    {
        if (!IsSupported || manager is null) return;
        try
        {
            var info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null || info.TargetFullRelease is null)
            {
                pending = null;
                UpToDate?.Invoke();
                return;
            }
            pending = info;
            LogUpdateAvailable(log, AvailableVersion);
            UpdateAvailable?.Invoke();
        }
        catch (Exception ex)
        {
            LogCheckFailed(log, ex);
            UpdateFailed?.Invoke(ex);
        }
    }

    public async Task ApplyAndRestartAsync()
    {
        if (!IsSupported || manager is null || pending is null) return;
        try
        {
            await manager.DownloadUpdatesAsync(pending).ConfigureAwait(false);
            manager.ApplyUpdatesAndRestart(pending);
        }
        catch (Exception ex)
        {
            LogApplyFailed(log, ex);
            UpdateFailed?.Invoke(ex);
        }
    }

    public void Dispose() => periodic?.Dispose();

    [LoggerMessage(Level = LogLevel.Information, Message = "UpdateManager unavailable — likely running outside an installed build")]
    private static partial void LogManagerUnavailable(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update available: {Version}")]
    private static partial void LogUpdateAvailable(ILogger logger, string? version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Update check failed")]
    private static partial void LogCheckFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Apply update failed")]
    private static partial void LogApplyFailed(ILogger logger, Exception ex);
}
