// tests/Orbital.Core.Tests/Persistence/AppPathsTests.cs
namespace Orbital.Core.Tests.Persistence;

using FluentAssertions;
using Orbital.Core.Persistence;
using Xunit;

public sealed class AppPathsTests
{
    [Fact]
    public void DataDirectory_is_not_empty_and_ends_with_Orbital()
    {
        var dir = AppPaths.DataDirectory;
        dir.Should().NotBeNullOrEmpty();
        Path.GetFileName(dir).Should().Be("Orbital");
    }

    [Fact]
    public void TodosFile_is_inside_DataDirectory()
    {
        AppPaths.TodosFile.Should().StartWith(AppPaths.DataDirectory);
        Path.GetFileName(AppPaths.TodosFile).Should().Be("todos.json");
    }

    [Fact]
    public void SettingsFile_is_inside_DataDirectory()
    {
        AppPaths.SettingsFile.Should().StartWith(AppPaths.DataDirectory);
        Path.GetFileName(AppPaths.SettingsFile).Should().Be("settings.json");
    }
}
