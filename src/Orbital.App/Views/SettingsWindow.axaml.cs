// src/Orbital.App/Views/SettingsWindow.axaml.cs
namespace Orbital.App.Views;

using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public partial class SettingsWindow : Window
{
    public event Action? SaveRequested;
    public event Action? CancelRequested;

    public SettingsWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("SaveButton")!.Click += (_, _) => SaveRequested?.Invoke();
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => CancelRequested?.Invoke();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
