// src/Orbital.App/Services/IDialogService.cs
namespace Orbital.App.Services;

using System.Threading.Tasks;

public interface IDialogService
{
    Task ShowErrorAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "OK", string cancelLabel = "Cancel");
}
