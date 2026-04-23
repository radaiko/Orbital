// tests/Orbital.Core.Tests/Ordering/TodoOrderingTests.cs
namespace Orbital.Core.Tests.Ordering;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.Ordering;
using Xunit;

public sealed class TodoOrderingTests
{
    private static Todo Make(int order) => new()
    {
        Id = Guid.NewGuid(),
        Title = $"t{order}",
        CreatedAt = DateTimeOffset.UtcNow,
        Order = order,
    };

    [Fact]
    public void NextTopOrder_returns_zero_when_list_empty()
    {
        TodoOrdering.NextTopOrder(Array.Empty<Todo>()).Should().Be(0);
    }

    [Fact]
    public void NextTopOrder_returns_min_minus_one()
    {
        var todos = new[] { Make(5), Make(-2), Make(0) };
        TodoOrdering.NextTopOrder(todos).Should().Be(-3);
    }

    [Fact]
    public void ReorderActive_moves_item_to_target_index()
    {
        var a = Make(0);
        var b = Make(1);
        var c = Make(2);
        var d = Make(3);
        var list = new List<Todo> { a, b, c, d };

        TodoOrdering.ReorderActive(list, fromIndex: 0, toIndex: 2);

        list.Select(t => t.Title).Should().Equal("t1", "t2", "t0", "t3");
        list.Select(t => t.Order).Should().BeInAscendingOrder();
    }

    [Fact]
    public void ReorderActive_is_noop_when_from_equals_to()
    {
        var list = new List<Todo> { Make(0), Make(1) };
        TodoOrdering.ReorderActive(list, 1, 1);
        list.Select(t => t.Title).Should().Equal("t0", "t1");
    }
}
