// tests/Orbital.Core.Tests/Persistence/JsonSettingsStoreTests.cs
namespace Orbital.Core.Tests.Persistence;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.Persistence;
using Xunit;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string tempDir;
    private readonly string file;
    private readonly JsonSettingsStore store;

    public JsonSettingsStoreTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "orbital-settings-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        file = Path.Combine(tempDir, "settings.json");
        store = new JsonSettingsStore(file);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public async Task Load_returns_defaults_when_file_missing()
    {
        var settings = await store.LoadAsync();
        settings.QuickAddHotkey.KeyName.Should().Be("T");
        settings.ToggleOverlayHotkey.KeyName.Should().Be("L");
        settings.OverlayPosition.Should().Be(OverlayPosition.TopRight);
        settings.StartAtLogin.Should().BeFalse();
    }

    [Fact]
    public async Task Save_then_load_round_trips_non_defaults()
    {
        var s = new AppSettings
        {
            QuickAddHotkey = new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Shift, "N"),
            OverlayPosition = OverlayPosition.BottomLeft,
            ShowCompleted = true,
            StartAtLogin = true,
        };
        await store.SaveAsync(s);
        var loaded = await store.LoadAsync();
        loaded.Should().BeEquivalentTo(s);
    }
}
