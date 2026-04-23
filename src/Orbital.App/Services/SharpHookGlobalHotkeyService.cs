// src/Orbital.App/Services/SharpHookGlobalHotkeyService.cs
namespace Orbital.App.Services;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Avalonia.Threading;
using Orbital.Core.Models;
using SharpHook;
using SharpHook.Native;

public sealed class SharpHookGlobalHotkeyService : IGlobalHotkeyService
{
    private readonly TaskPoolGlobalHook hook = new();
    private readonly ConcurrentDictionary<Guid, Registration> registrations = new();

    private sealed record Registration(HotkeyBinding Binding, Action Callback);

    public Task StartAsync()
    {
        hook.KeyPressed += OnKeyPressed;
        return hook.RunAsync();
    }

    public IDisposable Register(HotkeyBinding binding, Action onPressed)
    {
        var id = Guid.NewGuid();
        registrations[id] = new Registration(binding, onPressed);
        return new UnregisterHandle(() => registrations.TryRemove(id, out _));
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var modifiers = CurrentModifiers(e.RawEvent.Mask);
        var keyName = KeyCodeToName(e.Data.KeyCode);
        if (keyName is null) return;

        foreach (var reg in registrations.Values)
        {
            if (reg.Binding.Modifiers == modifiers &&
                string.Equals(reg.Binding.KeyName, keyName, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(reg.Callback);
            }
        }
    }

    private static HotkeyModifiers CurrentModifiers(ModifierMask mask)
    {
        var mods = HotkeyModifiers.None;
        if ((mask & (ModifierMask.LeftCtrl | ModifierMask.RightCtrl)) != 0)   mods |= HotkeyModifiers.Control;
        if ((mask & (ModifierMask.LeftShift | ModifierMask.RightShift)) != 0) mods |= HotkeyModifiers.Shift;
        if ((mask & (ModifierMask.LeftAlt | ModifierMask.RightAlt)) != 0)     mods |= HotkeyModifiers.Alt;
        if ((mask & (ModifierMask.LeftMeta | ModifierMask.RightMeta)) != 0)   mods |= HotkeyModifiers.Meta;
        return mods;
    }

    private static string? KeyCodeToName(KeyCode code) => code switch
    {
        KeyCode.VcA => "A", KeyCode.VcB => "B", KeyCode.VcC => "C", KeyCode.VcD => "D",
        KeyCode.VcE => "E", KeyCode.VcF => "F", KeyCode.VcG => "G", KeyCode.VcH => "H",
        KeyCode.VcI => "I", KeyCode.VcJ => "J", KeyCode.VcK => "K", KeyCode.VcL => "L",
        KeyCode.VcM => "M", KeyCode.VcN => "N", KeyCode.VcO => "O", KeyCode.VcP => "P",
        KeyCode.VcQ => "Q", KeyCode.VcR => "R", KeyCode.VcS => "S", KeyCode.VcT => "T",
        KeyCode.VcU => "U", KeyCode.VcV => "V", KeyCode.VcW => "W", KeyCode.VcX => "X",
        KeyCode.VcY => "Y", KeyCode.VcZ => "Z",
        KeyCode.VcF1 => "F1", KeyCode.VcF2 => "F2", KeyCode.VcF3 => "F3", KeyCode.VcF4 => "F4",
        KeyCode.VcF5 => "F5", KeyCode.VcF6 => "F6", KeyCode.VcF7 => "F7", KeyCode.VcF8 => "F8",
        KeyCode.VcF9 => "F9", KeyCode.VcF10 => "F10", KeyCode.VcF11 => "F11", KeyCode.VcF12 => "F12",
        KeyCode.VcSpace => "Space",
        _ => null,
    };

    public ValueTask DisposeAsync()
    {
        // Clear registrations first so any in-flight UI-thread posts see an
        // empty handler set and become no-ops.
        registrations.Clear();
        hook.KeyPressed -= OnKeyPressed;
        hook.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class UnregisterHandle(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
