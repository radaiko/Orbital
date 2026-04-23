// tests/Orbital.Core.Tests/Persistence/JsonTodoStoreTests.cs
namespace Orbital.Core.Tests.Persistence;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.Persistence;
using Xunit;

public sealed class JsonTodoStoreTests : IDisposable
{
    private readonly string tempDir;
    private readonly string todosFile;
    private readonly JsonTodoStore store;

    public JsonTodoStoreTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "orbital-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        todosFile = Path.Combine(tempDir, "todos.json");
        store = new JsonTodoStore(todosFile);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public async Task Load_returns_empty_when_file_does_not_exist()
    {
        var todos = await store.LoadAsync();
        todos.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_then_load_round_trips()
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            DueDate = new DateOnly(2026, 4, 24),
            CreatedAt = DateTimeOffset.Parse("2026-04-23T09:12:00+00:00", System.Globalization.CultureInfo.InvariantCulture),
            Order = 0,
        };
        await store.SaveAsync(new[] { todo });
        var loaded = await store.LoadAsync();
        loaded.Should().HaveCount(1);
        loaded[0].Should().BeEquivalentTo(todo);
    }

    [Fact]
    public async Task Save_is_atomic_leaves_no_tmp_file_on_success()
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(), Title = "x", CreatedAt = DateTimeOffset.UtcNow, Order = 0,
        };
        await store.SaveAsync(new[] { todo });
        File.Exists(todosFile).Should().BeTrue();
        File.Exists(todosFile + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task Load_is_tolerant_of_empty_file()
    {
        await File.WriteAllTextAsync(todosFile, "");
        var loaded = await store.LoadAsync();
        loaded.Should().BeEmpty();
    }

    [Fact]
    public async Task Load_throws_on_corrupt_json()
    {
        await File.WriteAllTextAsync(todosFile, "{not json");
        Func<Task> act = async () => await store.LoadAsync();
        await act.Should().ThrowAsync<Exception>();
    }
}
