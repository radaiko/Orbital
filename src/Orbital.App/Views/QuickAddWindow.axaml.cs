// src/Orbital.App/Views/QuickAddWindow.axaml.cs
namespace Orbital.App.Views;

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Orbital.Core.ViewModels;

public sealed partial class QuickAddWindow : Window
{
    public event Action? SubmitRequested;
    public event Action? CancelRequested;

    public QuickAddWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        Deactivated += (_, _) => CancelRequested?.Invoke();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                CancelRequested?.Invoke();
                break;
            case Key.Enter:
                e.Handled = true;
                SubmitRequested?.Invoke();
                break;
        }
    }

    public void FocusTitle()
    {
        var titleBox = this.FindControl<TextBox>("TitleBox");
        titleBox?.Focus();
        titleBox?.SelectAll();
    }
}
