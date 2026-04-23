// src/Orbital.App/Services/IAutoStartService.cs
namespace Orbital.App.Services;

public interface IAutoStartService
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}
