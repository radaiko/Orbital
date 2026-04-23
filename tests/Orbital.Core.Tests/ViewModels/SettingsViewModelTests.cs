// tests/Orbital.Core.Tests/ViewModels/SettingsViewModelTests.cs
namespace Orbital.Core.Tests.ViewModels;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;
using Xunit;

public sealed class SettingsViewModelTests
{
    private static AppSettings DefaultSettings() => new AppSettings();

    [Fact]
    public void Build_returns_settings_with_current_values()
    {
        var vm = new SettingsViewModel(DefaultSettings());
        vm.OverlayPosition = OverlayPosition.BottomLeft;

        var result = vm.Build();

        result.OverlayPosition.Should().Be(OverlayPosition.BottomLeft);
    }

    [Fact]
    public void ParseOrKeep_parses_Ctrl_Shift_letter_via_Build()
    {
        var vm = new SettingsViewModel(DefaultSettings());
        vm.QuickAddHotkey = "Ctrl+Shift+N";

        var result = vm.Build();

        result.QuickAddHotkey.Modifiers.Should().HaveFlag(HotkeyModifiers.Control);
        result.QuickAddHotkey.Modifiers.Should().HaveFlag(HotkeyModifiers.Shift);
        result.QuickAddHotkey.KeyName.Should().Be("N");
    }

    [Fact]
    public void ParseOrKeep_falls_back_to_original_on_empty_input()
    {
        var original = DefaultSettings();
        var vm = new SettingsViewModel(original);
        vm.QuickAddHotkey = "";

        var result = vm.Build();

        result.QuickAddHotkey.Should().Be(original.QuickAddHotkey);
    }

    [Fact]
    public void ParseOrKeep_falls_back_when_no_key_letter_provided()
    {
        var original = DefaultSettings();
        var vm = new SettingsViewModel(original);
        vm.QuickAddHotkey = "Ctrl+Alt"; // modifiers only, no key

        var result = vm.Build();

        result.QuickAddHotkey.Should().Be(original.QuickAddHotkey);
    }

    [Fact]
    public void Build_preserves_boolean_settings()
    {
        var vm = new SettingsViewModel(DefaultSettings());
        vm.OverlayAutoHideOnFocusLoss = false;
        vm.ShowCompleted = true;
        vm.StartAtLogin = true;

        var result = vm.Build();

        result.OverlayAutoHideOnFocusLoss.Should().BeFalse();
        result.ShowCompleted.Should().BeTrue();
        result.StartAtLogin.Should().BeTrue();
    }
}
