// src/Orbital.App/Views/OverlayWindow.axaml.cs
namespace Orbital.App.Views;

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Orbital.Core.ViewModels;

public sealed partial class OverlayWindow : Window
{
    public event Action? CloseRequested;
    public event Action? SettingsRequested;

    private bool isDragging;
    private int dragFromIndex = -1;
    private Point dragStart;

    public OverlayWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        // Deactivated is wired up by OverlayController so it can honour the
        // OverlayAutoHideOnFocusLoss setting at event time.
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => CloseRequested?.Invoke();
        this.FindControl<Button>("SettingsButton")!.Click += (_, _) => SettingsRequested?.Invoke();

        var list = this.FindControl<ListBox>("RowList")!;
        list.AddHandler(PointerPressedEvent, OnListPointerPressed, RoutingStrategies.Tunnel);
        list.AddHandler(PointerMovedEvent, OnListPointerMoved, RoutingStrategies.Tunnel);
        list.AddHandler(PointerReleasedEvent, OnListPointerReleased, RoutingStrategies.Tunnel);
        // If the pointer is released off the window (e.g. user alt-tabs mid-drag),
        // reset the drag state so the next press doesn't resurrect a stale from-index.
        list.AddHandler(PointerCaptureLostEvent, (_, _) => ResetDragState(), RoutingStrategies.Bubble);
        // Avalonia 12 does not expose ItemContainerGenerator.IndexChanged; use LayoutUpdated
        // (fires after each layout pass) to keep chip variant classes in sync.
        list.LayoutUpdated += (_, _) => RefreshChipClasses();
    }

    private void ResetDragState()
    {
        isDragging = false;
        dragFromIndex = -1;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var list = this.FindControl<ListBox>("RowList");
        var vm = (OverlayViewModel)this.DataContext!;
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                // If a row is being edited, Esc cancels the edit and stays open.
                // Otherwise Esc hides the overlay.
                if (list?.SelectedItem is TodoRowViewModel rEsc && rEsc.IsEditingTitle)
                    rEsc.CancelEditTitle();
                else if (list?.SelectedItem is TodoRowViewModel rEscDue && rEscDue.IsEditingDue)
                    rEscDue.CancelEditDue();
                else
                    CloseRequested?.Invoke();
                break;
            case Key.Space:
                if (list?.SelectedItem is TodoRowViewModel r1) { vm.ToggleComplete(r1); e.Handled = true; }
                break;
            case Key.Delete:
            case Key.Back:
                if (list?.SelectedItem is TodoRowViewModel r2) { vm.Delete(r2); e.Handled = true; }
                break;
            case Key.S:
                if (list?.SelectedItem is TodoRowViewModel r3) { vm.Snooze(r3); e.Handled = true; }
                break;
            case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta):
                vm.Undo(); e.Handled = true;
                break;
            case Key.Enter:
                if (list?.SelectedItem is TodoRowViewModel re)
                {
                    if (re.IsEditingTitle) { re.CommitTitle(re.Title); e.Handled = true; }
                    else if (re.IsEditingDue) { re.TryCommitDue(); e.Handled = true; }
                    else { re.BeginEditTitle(); e.Handled = true; }
                }
                break;
            case Key.D when e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta):
                if (list?.SelectedItem is TodoRowViewModel rd) { rd.BeginEditDue(); e.Handled = true; }
                break;
        }
    }

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var list = (ListBox)sender!;
        var item = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item is null) return;
        dragFromIndex = list.IndexFromContainer(item);
        dragStart = e.GetPosition(list);
    }

    private void OnListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (dragFromIndex < 0) return;
        var list = (ListBox)sender!;
        var pos = e.GetPosition(list);
        if (!isDragging && Point.Distance(pos, dragStart) > 6)
            isDragging = true;
    }

    private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (!isDragging || dragFromIndex < 0) return;
            var list = (ListBox)sender!;
            var pos = e.GetPosition(list);
            // Compute drop index as the ListBoxItem under the pointer, clamped to valid range.
            var hit = list.GetVisualsAt(pos)
                          .Select(v => v.FindAncestorOfType<ListBoxItem>())
                          .FirstOrDefault(x => x is not null);
            int toIndex = hit is not null ? list.IndexFromContainer(hit) : list.ItemCount - 1;
            if (toIndex < 0) toIndex = 0;

            if (DataContext is OverlayViewModel vm && toIndex != dragFromIndex)
                vm.Reorder(dragFromIndex, toIndex);
        }
        finally
        {
            ResetDragState();
        }
    }

    private void RefreshChipClasses()
    {
        var list = this.FindControl<ListBox>("RowList");
        if (list is null) return;
        foreach (var item in list.GetLogicalDescendants().OfType<Border>())
        {
            if (!item.Classes.Contains("chip")) continue;
            var row = item.DataContext as TodoRowViewModel;
            if (row is null) continue;
            var hasToday   = item.Classes.Contains("today");
            var hasOverdue = item.Classes.Contains("overdue");
            if (row.IsOverdue)
            {
                if (!hasOverdue) item.Classes.Add("overdue");
                if (hasToday)    item.Classes.Remove("today");
            }
            else if (row.IsDueToday)
            {
                if (!hasToday)   item.Classes.Add("today");
                if (hasOverdue)  item.Classes.Remove("overdue");
            }
            else
            {
                if (hasOverdue) item.Classes.Remove("overdue");
                if (hasToday)   item.Classes.Remove("today");
            }
        }
    }
}
