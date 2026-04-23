// src/Orbital.App/Views/AboutWindow.axaml.cs
namespace Orbital.App.Views;

using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public sealed partial class AboutWindow : Window
{
    public Func<bool>? CheckUpdatesRequested { get; set; }

    public AboutWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("CheckUpdatesButton")!.Click += OnCheckUpdates;
        this.FindControl<Button>("GitHubButton")!.Click += (_, _) => OpenUrl("https://github.com/radaiko/Orbital");
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* best-effort */ }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCheckUpdates(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("UpdateStatus")!;
        status.Text = "Checking…";
        try
        {
            var found = CheckUpdatesRequested?.Invoke() ?? false;
            status.Text = found ? "Update available — see tray menu." : "You're up to date.";
        }
        catch (Exception ex)
        {
            status.Text = $"Check failed: {ex.Message}";
        }
    }
}
