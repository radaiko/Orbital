// tests/Orbital.Core.Tests/ViewModels/TodoRowViewModelTests.cs
namespace Orbital.Core.Tests.ViewModels;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;
using Xunit;

public sealed class TodoRowViewModelTests
{
    private static Todo MakeTodo(DateOnly? due = null, bool completed = false) => new()
    {
        Id = Guid.NewGuid(),
        Title = "x",
        DueDate = due,
        CreatedAt = DateTimeOffset.UtcNow,
        Order = 0,
        CompletedAt = completed ? DateTimeOffset.UtcNow : null,
    };

    [Fact]
    public void Overdue_when_due_before_today()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(new DateOnly(2026, 4, 20)), () => today);
        vm.IsOverdue.Should().BeTrue();
        vm.IsDueToday.Should().BeFalse();
    }

    [Fact]
    public void Due_today_when_due_equals_today()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(today), () => today);
        vm.IsDueToday.Should().BeTrue();
        vm.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void Completed_not_overdue()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(new DateOnly(2026, 4, 20), completed: true), () => today);
        vm.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void DueLabel_shows_em_dash_when_no_due()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(null), () => today);
        vm.DueLabel.Should().Be("—");
    }

    [Fact]
    public void DueLabel_shows_Today_or_Tomorrow_or_weekday_or_full()
    {
        var today = new DateOnly(2026, 4, 23); // Thu
        new TodoRowViewModel(MakeTodo(today), () => today).DueLabel.Should().Be("Today");
        new TodoRowViewModel(MakeTodo(today.AddDays(1)), () => today).DueLabel.Should().Be("Tomorrow");
        new TodoRowViewModel(MakeTodo(today.AddDays(2)), () => today).DueLabel.Should().Be("Sat");
        new TodoRowViewModel(MakeTodo(today.AddDays(8)), () => today).DueLabel.Should().Be("May 1"); // not a weekday rendering — >7 days
    }
}
