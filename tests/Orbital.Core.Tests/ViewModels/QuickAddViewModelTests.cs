// tests/Orbital.Core.Tests/ViewModels/QuickAddViewModelTests.cs
namespace Orbital.Core.Tests.ViewModels;

using FluentAssertions;
using Orbital.Core.DateParsing;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;
using Xunit;

public sealed class QuickAddViewModelTests
{
    private static QuickAddViewModel MakeVm(DateOnly today)
    {
        var parser = new DueDateParser(() => today);
        return new QuickAddViewModel(parser);
    }

    [Fact]
    public void Empty_title_means_cannot_submit()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "";
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void Valid_title_empty_due_means_can_submit()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "Buy milk";
        vm.DueInput = "";
        vm.CanSubmit.Should().BeTrue();
        vm.DueParsed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Valid_title_valid_due_means_can_submit_and_parses()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "x";
        vm.DueInput = "tomorrow";
        vm.CanSubmit.Should().BeTrue();
        vm.DueParsed.Date.Should().Be(new DateOnly(2026, 4, 24));
    }

    [Fact]
    public void Valid_title_invalid_due_blocks_submit()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "x";
        vm.DueInput = "asdf";
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void BuildTodo_returns_null_when_not_submittable()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "";
        vm.BuildTodo(order: 0).Should().BeNull();
    }

    [Fact]
    public void BuildTodo_creates_todo_with_parsed_due()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "Buy milk";
        vm.DueInput = "fri";
        var todo = vm.BuildTodo(order: 42);
        todo.Should().NotBeNull();
        todo!.Title.Should().Be("Buy milk");
        todo.DueDate.Should().Be(new DateOnly(2026, 4, 24));
        todo.Order.Should().Be(42);
    }

    [Fact]
    public void Reset_clears_fields()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "x";
        vm.DueInput = "tomorrow";
        vm.Reset();
        vm.Title.Should().BeEmpty();
        vm.DueInput.Should().BeEmpty();
    }
}
