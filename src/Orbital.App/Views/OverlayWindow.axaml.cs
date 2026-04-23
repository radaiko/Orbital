// src/Orbital.App/Views/OverlayWindow.axaml.cs
namespace Orbital.App.Views;

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Orbital.Core.ViewModels;

public sealed partial class OverlayWindow : Window
{
    public event Action? CloseRequested;
    public event Action? SettingsRequested;

    public OverlayWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        Deactivated += (_, _) => CloseRequested?.Invoke();
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => CloseRequested?.Invoke();
        this.FindControl<Button>("SettingsButton")!.Click += (_, _) => SettingsRequested?.Invoke();
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
        }
    }
}
