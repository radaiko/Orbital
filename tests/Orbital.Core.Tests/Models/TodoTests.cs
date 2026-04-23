// tests/Orbital.Core.Tests/Models/TodoTests.cs
namespace Orbital.Core.Tests.Models;

using FluentAssertions;
using Orbital.Core.Models;
using Xunit;

public sealed class TodoTests
{
    [Fact]
    public void Todo_is_constructable_with_required_fields()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var todo = new Todo
        {
            Id = id,
            Title = "Buy milk",
            CreatedAt = createdAt,
            Order = 0,
        };

        todo.Id.Should().Be(id);
        todo.Title.Should().Be("Buy milk");
        todo.CreatedAt.Should().Be(createdAt);
        todo.Order.Should().Be(0);
        todo.DueDate.Should().BeNull();
        todo.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Todo_active_vs_complete_is_computed_from_CompletedAt()
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            Title = "x",
            CreatedAt = DateTimeOffset.UtcNow,
            Order = 0,
        };
        todo.IsCompleted.Should().BeFalse();
        todo.CompletedAt = DateTimeOffset.UtcNow;
        todo.IsCompleted.Should().BeTrue();
    }
}
