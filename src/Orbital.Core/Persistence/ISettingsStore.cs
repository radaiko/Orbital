// src/Orbital.Core/Persistence/ISettingsStore.cs
namespace Orbital.Core.Persistence;

using System.Threading;
using System.Threading.Tasks;
using Orbital.Core.Models;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads settings and reports whether the settings file previously existed.
    /// If <c>existed</c> is false, the returned settings are the defaults and no file has ever been written.
    /// </summary>
    Task<(AppSettings settings, bool existed)> LoadWithProvenanceAsync(CancellationToken ct = default);

    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
