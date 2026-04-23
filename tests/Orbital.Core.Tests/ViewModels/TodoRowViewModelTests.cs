// tests/Orbital.Core.Tests/ViewModels/TodoRowViewModelTests.cs
namespace Orbital.Core.Tests.ViewModels;

using FluentAssertions;
using Orbital.Core.DateParsing;
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
        var vm = new TodoRowViewModel(MakeTodo(new DateOnly(2026, 4, 20)), null, () => today);
        vm.IsOverdue.Should().BeTrue();
        vm.IsDueToday.Should().BeFalse();
    }

    [Fact]
    public void Due_today_when_due_equals_today()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(today), null, () => today);
        vm.IsDueToday.Should().BeTrue();
        vm.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void Completed_not_overdue()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(new DateOnly(2026, 4, 20), completed: true), null, () => today);
        vm.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void DueLabel_shows_em_dash_when_no_due()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(null), null, () => today);
        vm.DueLabel.Should().Be("—");
    }

    [Fact]
    public void DueLabel_shows_Today_or_Tomorrow_or_weekday_or_full()
    {
        var today = new DateOnly(2026, 4, 23); // Thu
        new TodoRowViewModel(MakeTodo(today), null, () => today).DueLabel.Should().Be("Today");
        new TodoRowViewModel(MakeTodo(today.AddDays(1)), null, () => today).DueLabel.Should().Be("Tomorrow");
        new TodoRowViewModel(MakeTodo(today.AddDays(2)), null, () => today).DueLabel.Should().Be("Sat");
        new TodoRowViewModel(MakeTodo(today.AddDays(8)), null, () => today).DueLabel.Should().Be("May 1"); // not a weekday rendering — >7 days
    }

    [Fact]
    public void TryCommitDue_parses_and_sets_date()
    {
        var today = new DateOnly(2026, 4, 23);
        var todo = new Todo { Id = Guid.NewGuid(), Title = "x", CreatedAt = DateTimeOffset.UtcNow, Order = 0 };
        var vm = new TodoRowViewModel(todo, new DueDateParser(() => today), () => today);
        vm.BeginEditDue();
        vm.DueEditBuffer = "tomorrow";
        vm.TryCommitDue().Should().BeTrue();
        vm.DueDate.Should().Be(new DateOnly(2026, 4, 24));
        vm.IsEditingDue.Should().BeFalse();
    }

    [Fact]
    public void TryCommitDue_returns_false_on_invalid()
    {
        var today = new DateOnly(2026, 4, 23);
        var todo = new Todo { Id = Guid.NewGuid(), Title = "x", CreatedAt = DateTimeOffset.UtcNow, Order = 0 };
        var vm = new TodoRowViewModel(todo, new DueDateParser(() => today), () => today);
        vm.BeginEditDue();
        vm.DueEditBuffer = "asdf";
        vm.TryCommitDue().Should().BeFalse();
        vm.IsEditingDue.Should().BeTrue();
    }

    [Fact]
    public void CancelEditTitle_reverts_intermediate_TwoWay_mutations()
    {
        // The title TextBox in the overlay XAML is TwoWay-bound, so every
        // keystroke updates Model.Title. Cancel must restore the pre-edit value.
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            CreatedAt = DateTimeOffset.UtcNow,
            Order = 0,
        };
        var vm = new TodoRowViewModel(todo);
        vm.BeginEditTitle();
        vm.Title = "Buy milkzzzz"; // simulate keystrokes via TwoWay binding
        vm.CancelEditTitle();
        vm.Title.Should().Be("Buy milk");
        vm.IsEditingTitle.Should().BeFalse();
    }
}
