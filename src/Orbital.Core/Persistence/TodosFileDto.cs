// src/Orbital.Core/Persistence/TodosFileDto.cs
namespace Orbital.Core.Persistence;

using System.Text.Json.Serialization;
using Orbital.Core.Models;

internal sealed record TodosFileDto
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("todos")]         public List<TodoDto> Todos { get; init; } = new();
}

internal sealed record TodoDto
{
    [JsonPropertyName("id")]          public Guid Id { get; init; }
    [JsonPropertyName("title")]       public string Title { get; init; } = "";
    [JsonPropertyName("dueDate")]     public DateOnly? DueDate { get; init; }
    [JsonPropertyName("createdAt")]   public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("completedAt")] public DateTimeOffset? CompletedAt { get; init; }
    [JsonPropertyName("order")]       public int Order { get; init; }

    public static TodoDto From(Todo t) => new()
    {
        Id = t.Id, Title = t.Title, DueDate = t.DueDate,
        CreatedAt = t.CreatedAt, CompletedAt = t.CompletedAt, Order = t.Order,
    };

    public Todo ToDomain() => new()
    {
        Id = Id, Title = Title, DueDate = DueDate,
        CreatedAt = CreatedAt, CompletedAt = CompletedAt, Order = Order,
    };
}
