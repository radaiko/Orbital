// tests/Orbital.Core.Tests/ViewModels/OverlayViewModelTests.cs
namespace Orbital.Core.Tests.ViewModels;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;
using System.Collections.ObjectModel;
using Xunit;

public sealed class OverlayViewModelTests
{
    private static Todo T(int order, string title, bool completed = false) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        CreatedAt = DateTimeOffset.UtcNow,
        Order = order,
        CompletedAt = completed ? DateTimeOffset.UtcNow : null,
    };

    private static OverlayViewModel Make(ObservableCollection<Todo> todos, bool showCompleted = false)
    {
        var vm = new OverlayViewModel(todos, () => new DateOnly(2026, 4, 23))
        {
            ShowCompleted = showCompleted,
        };
        return vm;
    }

    [Fact]
    public void Rows_shows_only_active_sorted_by_order_ascending()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(2, "c"), T(0, "a"), T(1, "b"), T(-1, "first"),
        };
        var vm = Make(todos);
        vm.Rows.Select(r => r.Title).Should().Equal("first", "a", "b", "c");
    }

    [Fact]
    public void Completed_items_hidden_by_default_visible_when_ShowCompleted()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "done", completed: true), T(1, "active"),
        };
        var vm = Make(todos);
        vm.Rows.Select(r => r.Title).Should().Equal("active");

        vm.ShowCompleted = true;
        vm.Rows.Select(r => r.Title).Should().Equal("active", "done");
    }

    [Fact]
    public void ToggleComplete_moves_row_out_of_active_when_ShowCompleted_is_false()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"), T(1, "b"),
        };
        var vm = Make(todos);
        vm.ToggleComplete(vm.Rows[0]);
        vm.Rows.Select(r => r.Title).Should().Equal("b");
    }

    [Fact]
    public void Delete_removes_from_todos_and_rows()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"), T(1, "b"),
        };
        var vm = Make(todos);
        vm.Delete(vm.Rows[0]);
        todos.Select(t => t.Title).Should().Equal("b");
        vm.Rows.Select(r => r.Title).Should().Equal("b");
    }

    [Fact]
    public void Undo_after_delete_restores_row()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"),
        };
        var vm = Make(todos);
        vm.Delete(vm.Rows[0]);
        vm.Undo();
        todos.Should().ContainSingle(t => t.Title == "a");
        vm.Rows.Should().ContainSingle();
    }

    [Fact]
    public void Mutations_raise_TodosMutated_so_controller_can_save()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"), T(1, "b"),
        };
        var vm = Make(todos);
        int count = 0;
        vm.TodosMutated += () => count++;

        vm.ToggleComplete(vm.Rows[0]);
        count.Should().Be(1, "ToggleComplete should fire TodosMutated");

        vm.Delete(vm.Rows[0]);
        count.Should().Be(2, "Delete should fire TodosMutated");

        vm.Undo();
        count.Should().Be(3, "Undo should fire TodosMutated");

        vm.Snooze(vm.Rows[0]);
        count.Should().Be(4, "Snooze should fire TodosMutated");
    }

    [Fact]
    public void Reorder_changes_row_order()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"), T(1, "b"), T(2, "c"),
        };
        var vm = Make(todos);
        vm.Reorder(0, 2);
        vm.Rows.Select(r => r.Title).Should().Equal("b", "c", "a");
    }

    [Fact]
    public void Reorder_updates_persisted_order_values_sequentially()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"), T(1, "b"), T(2, "c"),
        };
        var vm = Make(todos);
        vm.Reorder(0, 2);
        // After reorder: b=0, c=1, a=2 (TodoOrdering.ReorderActive reassigns sequential Orders)
        vm.Rows[0].Model.Order.Should().Be(0);
        vm.Rows[1].Model.Order.Should().Be(1);
        vm.Rows[2].Model.Order.Should().Be(2);
    }

    [Fact]
    public void Rebuild_preserves_row_vm_identity_when_other_row_mutates()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"), T(1, "b"),
        };
        var vm = Make(todos);

        // Start editing the first row
        vm.Rows[0].BeginEditTitle();
        vm.Rows[0].IsEditingTitle.Should().BeTrue();

        // Mutate a different row (toggle complete on row "b"), which triggers Rebuild
        vm.ToggleComplete(vm.Rows[1]);

        // Row "a" should still be in editing state
        vm.Rows[0].IsEditingTitle.Should().BeTrue("Rebuild must preserve row VM identity");
    }
}
