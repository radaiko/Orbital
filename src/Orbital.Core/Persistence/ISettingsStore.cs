// src/Orbital.Core/Persistence/ISettingsStore.cs
namespace Orbital.Core.Persistence;

using Orbital.Core.Models;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
