// src/Orbital.App/Services/DialogService.cs
namespace Orbital.App.Services;

using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Orbital.App.Views;

public sealed class DialogService : IDialogService
{
    public Task ShowErrorAsync(string title, string message) =>
        RunAsync(title, message, "OK", cancelLabel: null).ContinueWith(_ => { });

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "OK", string cancelLabel = "Cancel") =>
        RunAsync(title, message, confirmLabel, cancelLabel);

    private static async Task<bool> RunAsync(string title, string message, string confirmLabel, string? cancelLabel)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = (Avalonia.Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var w = new MessageWindow();
            return await w.ShowAsync(owner, title, message, confirmLabel, cancelLabel);
        });
    }
}
