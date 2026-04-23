// src/Orbital.Core/ViewModels/OverlayViewModel.cs
namespace Orbital.Core.ViewModels;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using Orbital.Core.Models;

public sealed partial class OverlayViewModel : ObservableObject
{
    private readonly ObservableCollection<Todo> source;
    private readonly Func<DateOnly> today;
    private Action? lastUndo;

    public ObservableCollection<TodoRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private bool showCompleted;

    public OverlayViewModel(ObservableCollection<Todo> source, Func<DateOnly>? todayProvider = null)
    {
        this.source = source;
        this.today = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Today));
        source.CollectionChanged += OnSourceChanged;
        Rebuild();
    }

    partial void OnShowCompletedChanged(bool value) => Rebuild();

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    public event Action? TodosMutated;

    private void Rebuild()
    {
        IEnumerable<Todo> active = source.Where(t => !t.IsCompleted).OrderBy(t => t.Order);
        IEnumerable<Todo> completed = ShowCompleted
            ? source.Where(t => t.IsCompleted).OrderByDescending(t => t.CompletedAt)
            : Enumerable.Empty<Todo>();

        Rows.Clear();
        foreach (var t in active.Concat(completed))
            Rows.Add(new TodoRowViewModel(t, today));
    }

    public void ToggleComplete(TodoRowViewModel row)
    {
        row.IsCompleted = !row.IsCompleted;
        TodosMutated?.Invoke();
        Rebuild();
    }

    public void Delete(TodoRowViewModel row)
    {
        var todo = row.Model;
        var index = source.IndexOf(todo);
        if (index < 0) return;
        source.RemoveAt(index); // triggers OnSourceChanged -> Rebuild
        lastUndo = () =>
        {
            source.Insert(Math.Min(index, source.Count), todo);
            TodosMutated?.Invoke();
        };
        TodosMutated?.Invoke();
    }

    public void Snooze(TodoRowViewModel row)
    {
        var t = today();
        row.DueDate = row.DueDate is { } d ? d.AddDays(1) : t.AddDays(1);
        TodosMutated?.Invoke();
    }

    public void Undo()
    {
        var action = lastUndo;
        lastUndo = null;
        action?.Invoke();
    }
}
