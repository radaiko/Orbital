// src/Orbital.Core/Persistence/ITodoStore.cs
namespace Orbital.Core.Persistence;

using Orbital.Core.Models;

public interface ITodoStore
{
    Task<IReadOnlyList<Todo>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IReadOnlyList<Todo> todos, CancellationToken ct = default);
}
