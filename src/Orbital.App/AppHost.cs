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
    private readonly Lock debounceLock = new();
    private CancellationTokenSource? debounceCts;
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
        // Debounce: cancel any in-flight delay, start a fresh one. Only the
        // last scheduled save actually writes to disk.
        CancellationTokenSource cts;
        lock (debounceLock)
        {
            debounceCts?.Cancel();
            debounceCts?.Dispose();
            debounceCts = new CancellationTokenSource();
            cts = debounceCts;
        }
        pendingSave = RunDebouncedSave(cts.Token);
    }

    private async Task RunDebouncedSave(CancellationToken ct)
    {
        try { await Task.Delay(250, ct); }
        catch (TaskCanceledException) { return; }
        await todoStore.SaveAsync(Todos.ToArray(), ct);
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
        lock (debounceLock)
        {
            debounceCts?.Cancel();
            debounceCts?.Dispose();
            debounceCts = null;
        }
        GC.SuppressFinalize(this);
    }
}
