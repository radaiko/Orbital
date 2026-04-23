// src/Orbital.App/Services/IGlobalHotkeyService.cs
namespace Orbital.App.Services;

using System;
using System.Threading.Tasks;
using Orbital.Core.Models;

public interface IGlobalHotkeyService : IAsyncDisposable
{
    Task StartAsync();
    IDisposable Register(HotkeyBinding binding, Action onPressed);
}
