// src/Orbital.App/Views/MessageWindow.axaml.cs
namespace Orbital.App.Views;

using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public sealed partial class MessageWindow : Window
{
    private TaskCompletionSource<bool> tcs = new();

    public MessageWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("ConfirmButton")!.Click += (_, _) => CloseWithResult(true);
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => CloseWithResult(false);
        Closed += (_, _) => tcs.TrySetResult(false);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public Task<bool> ShowAsync(Window? owner, string title, string message, string confirmLabel, string? cancelLabel)
    {
        Title = title;
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
        var confirm = this.FindControl<Button>("ConfirmButton")!;
        confirm.Content = confirmLabel;
        var cancel = this.FindControl<Button>("CancelButton")!;
        cancel.IsVisible = cancelLabel is not null;
        if (cancelLabel is not null) cancel.Content = cancelLabel;

        tcs = new TaskCompletionSource<bool>();
        if (owner is not null) ShowDialog(owner);
        else Show();
        return tcs.Task;
    }

    private void CloseWithResult(bool result)
    {
        tcs.TrySetResult(result);
        Close();
    }
}
