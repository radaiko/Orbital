// src/Orbital.App/Services/QuickAddController.cs
namespace Orbital.App.Services;

using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Orbital.App.Views;
using Orbital.Core.DateParsing;
using Orbital.Core.Ordering;
using Orbital.Core.ViewModels;

public sealed class QuickAddController
{
    private readonly AppHost host;
    private readonly DueDateParser parser = new();
    private QuickAddWindow? window;
    private QuickAddViewModel? vm;

    public QuickAddController(AppHost host) { this.host = host; }

    public void Toggle()
    {
        if (window is { IsVisible: true })
        {
            window.FocusTitle(); // re-invoke while open: focus & select (spec §9)
            window.Activate();
            return;
        }
        ShowNew();
    }

    private void ShowNew()
    {
        window ??= CreateWindow();
        vm!.Reset();
        PositionOnActiveScreen(window);
        window.Show();
        window.Activate();
        window.FocusTitle();
    }

    private QuickAddWindow CreateWindow()
    {
        vm = new QuickAddViewModel(parser);
        var w = new QuickAddWindow { DataContext = vm };
        w.SubmitRequested += OnSubmit;
        w.CancelRequested += () => w.Hide();
        return w;
    }

    private void OnSubmit()
    {
        if (vm is null || window is null) return;
        var order = TodoOrdering.NextTopOrder(host.Todos);
        var todo = vm.BuildTodo(order);
        if (todo is null) return;
        host.Todos.Insert(0, todo);
        host.ScheduleSaveTodos();
        window.Hide();
    }

    private static void PositionOnActiveScreen(Window w)
    {
        var screen = w.Screens.ScreenFromWindow(w) ?? w.Screens.Primary;
        if (screen is null) return;
        // WorkingArea and Position are in physical pixels; Width/Height are DIPs.
        // Multiply by scaling so HiDPI / Retina displays center correctly.
        var r = screen.WorkingArea;
        var scale = screen.Scaling;
        var physicalW = (int)(w.Width * scale);
        w.Position = new Avalonia.PixelPoint(
            r.X + (r.Width - physicalW) / 2,
            r.Y + r.Height / 3);
    }
}
