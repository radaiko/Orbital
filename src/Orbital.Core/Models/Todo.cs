// src/Orbital.Core/Models/Todo.cs
namespace Orbital.Core.Models;

public sealed record Todo
{
    public required Guid Id { get; init; }
    public required string Title { get; set; }
    public DateOnly? DueDate { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public required int Order { get; set; }

    public bool IsCompleted => CompletedAt is not null;
}
