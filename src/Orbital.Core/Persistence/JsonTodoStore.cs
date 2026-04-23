// src/Orbital.Core/Persistence/JsonTodoStore.cs
namespace Orbital.Core.Persistence;

using System.Text.Json;
using Orbital.Core.Models;

public sealed class JsonTodoStore : ITodoStore, IDisposable
{
    private readonly string filePath;
    private readonly JsonSerializerOptions options;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public JsonTodoStore(string? filePath = null)
    {
        this.filePath = filePath ?? AppPaths.TodosFile;
        options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public async Task<IReadOnlyList<Todo>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return Array.Empty<Todo>();

        var json = await File.ReadAllTextAsync(filePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<Todo>();

        var dto = JsonSerializer.Deserialize<TodosFileDto>(json, options)
                  ?? throw new InvalidDataException("todos.json deserialized to null");
        return dto.Todos.Select(d => d.ToDomain()).ToArray();
    }

    public void Dispose() => writeLock.Dispose();

    public async Task SaveAsync(IReadOnlyList<Todo> todos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(todos);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var dto = new TodosFileDto
        {
            SchemaVersion = 1,
            Todos = todos.Select(TodoDto.From).ToList(),
        };

        await writeLock.WaitAsync(ct);
        try
        {
            var tmpPath = filePath + ".tmp";
            await using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, dto, options, ct);
                await fs.FlushAsync(ct);
                fs.Flush(flushToDisk: true);
            }
            File.Move(tmpPath, filePath, overwrite: true);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
