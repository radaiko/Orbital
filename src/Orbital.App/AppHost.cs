// src/Orbital.App/AppHost.cs
namespace Orbital.App;

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orbital.Core.Models;
using Orbital.Core.Persistence;

public sealed class AppHost : IDisposable
{
    private readonly ITodoStore todoStore;
    private readonly ISettingsStore settingsStore;
    private readonly CancellationTokenSource debounceTokenSource = new();
    private Task? pendingSave;

    public ObservableCollection<Todo> Todos { get; } = new();
    public AppSettings Settings { get; private set; } = new();

    public event Action? SettingsChanged;

    public AppHost(ITodoStore? todoStore = null, ISettingsStore? settingsStore = null)
    {
        this.todoStore = todoStore ?? new JsonTodoStore();
        this.settingsStore = settingsStore ?? new JsonSettingsStore();
    }

    public async Task LoadAsync()
    {
        Settings = await settingsStore.LoadAsync();
        var todos = await todoStore.LoadAsync();
        Todos.Clear();
        foreach (var t in todos) Todos.Add(t);
    }

    public void ScheduleSaveTodos()
    {
        // Debounce: if called multiple times within 250ms, only the last save runs.
        var token = debounceTokenSource.Token;
        pendingSave = Task.Run(async () =>
        {
            try { await Task.Delay(250, token); }
            catch (TaskCanceledException) { return; }
            await todoStore.SaveAsync(Todos.ToArray());
        }, token);
    }

    public async Task SaveSettingsAsync()
    {
        await settingsStore.SaveAsync(Settings);
        SettingsChanged?.Invoke();
    }

    public async Task FlushAsync()
    {
        if (pendingSave is { } p) { try { await p; } catch { } }
        await todoStore.SaveAsync(Todos.ToArray());
        await settingsStore.SaveAsync(Settings);
    }

    public void Dispose()
    {
        debounceTokenSource.Cancel();
        debounceTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
