// src/Orbital.Core/ViewModels/QuickAddViewModel.cs
namespace Orbital.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Orbital.Core.DateParsing;
using Orbital.Core.Models;

public sealed partial class QuickAddViewModel : ObservableObject
{
    private readonly DueDateParser parser;

    public QuickAddViewModel(DueDateParser parser)
    {
        this.parser = parser;
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string dueInput = string.Empty;

    public ParseResult DueParsed => parser.Parse(DueInput);

    public bool CanSubmit =>
        !string.IsNullOrWhiteSpace(Title) && !DueParsed.IsError;

    partial void OnTitleChanged(string value) { OnPropertyChanged(nameof(CanSubmit)); }

    partial void OnDueInputChanged(string value)
    {
        OnPropertyChanged(nameof(DueParsed));
        OnPropertyChanged(nameof(CanSubmit));
    }

    public Todo? BuildTodo(int order)
    {
        if (!CanSubmit) return null;
        return new Todo
        {
            Id = Guid.NewGuid(),
            Title = Title.Trim(),
            DueDate = DueParsed.Date,
            CreatedAt = DateTimeOffset.Now,
            Order = order,
        };
    }

    public void Reset()
    {
        Title = string.Empty;
        DueInput = string.Empty;
    }
}
