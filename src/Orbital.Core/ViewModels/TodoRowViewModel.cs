// src/Orbital.Core/ViewModels/TodoRowViewModel.cs
namespace Orbital.Core.ViewModels;

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Orbital.Core.DateParsing;
using Orbital.Core.Models;

public sealed partial class TodoRowViewModel : ObservableObject
{
    private readonly Func<DateOnly> today;
    private readonly DueDateParser parser;

    public TodoRowViewModel(Todo model, DueDateParser? parser = null, Func<DateOnly>? todayProvider = null)
    {
        Model = model;
        today = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Today));
        this.parser = parser ?? new DueDateParser(today);
    }

    public Todo Model { get; }

    public string Title
    {
        get => Model.Title;
        set
        {
            if (Model.Title == value) return;
            Model.Title = value;
            OnPropertyChanged();
        }
    }

    public DateOnly? DueDate
    {
        get => Model.DueDate;
        set
        {
            if (Model.DueDate == value) return;
            Model.DueDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DueLabel));
            OnPropertyChanged(nameof(IsOverdue));
            OnPropertyChanged(nameof(IsDueToday));
        }
    }

    public bool IsCompleted
    {
        get => Model.IsCompleted;
        set
        {
            if (value == Model.IsCompleted) return;
            Model.CompletedAt = value ? DateTimeOffset.Now : null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverdue));
        }
    }

    public string DueLabel
    {
        get
        {
            if (DueDate is null) return "—";
            var d = DueDate.Value;
            var t = today();
            if (d == t) return "Today";
            if (d == t.AddDays(1)) return "Tomorrow";
            var diff = d.DayNumber - t.DayNumber;
            if (diff > 0 && diff < 7)
                return d.ToString("ddd", CultureInfo.InvariantCulture);
            return d.ToString("MMM d", CultureInfo.InvariantCulture);
        }
    }

    public bool IsOverdue => DueDate is { } d && !IsCompleted && d < today();
    public bool IsDueToday => DueDate is { } d && d == today();

    // --- Inline editing ---

    [ObservableProperty]
    private bool isEditingTitle;

    [ObservableProperty]
    private bool isEditingDue;

    [ObservableProperty]
    private string dueEditBuffer = string.Empty;

    public void BeginEditTitle() => IsEditingTitle = true;

    public void CommitTitle(string newTitle)
    {
        Title = newTitle.Trim();
        IsEditingTitle = false;
    }

    public void CancelEditTitle()
    {
        IsEditingTitle = false;
        OnPropertyChanged(nameof(Title));
    }

    public void BeginEditDue()
    {
        DueEditBuffer = DueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        IsEditingDue = true;
    }

    public bool TryCommitDue()
    {
        var r = parser.Parse(DueEditBuffer);
        if (r.IsError) return false;
        DueDate = r.Date;
        IsEditingDue = false;
        return true;
    }

    public void CancelEditDue() => IsEditingDue = false;
}
