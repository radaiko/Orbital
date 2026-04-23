// src/Orbital.App/Services/IUpdateService.cs
namespace Orbital.App.Services;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IUpdateService : IDisposable
{
    bool IsSupported { get; }
    bool IsUpdateAvailable { get; }
    string? AvailableVersion { get; }

    Task CheckAsync(CancellationToken ct = default);
    Task ApplyAndRestartAsync();

    event Action? UpdateAvailable;
    event Action? UpToDate;
    event Action<Exception>? UpdateFailed;
}
