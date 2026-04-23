# Orbital Todo App — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a keyboard-driven, local-first personal todo app for Windows and macOS. Two global hotkeys drive a quick-add popup and a translucent always-on-top overlay. JSON storage, manual drag-reorder, no priority field, no sync.

**Architecture:** Two-project solution — `Orbital.Core` holds pure domain (models, JSON store, due-date parser) with full unit tests; `Orbital.App` holds all Avalonia UI and platform glue (global hotkeys, tray icon, autostart). App starts with no main window; lives in the tray; lazy-creates and reuses quick-add and overlay windows.

**Tech Stack:** C# on .NET 10 (LTS) · Avalonia 12 · CommunityToolkit.Mvvm · SharpHook · System.Text.Json · xUnit · FluentAssertions.

**Reference spec:** `docs/superpowers/specs/2026-04-23-orbital-todo-design.md`. If the plan and spec conflict, the spec wins — update the plan and re-check.

**Platform note:** Development happens on macOS; target Windows and macOS both. Engineer should run `dotnet test` on every task and smoke-test UI changes on whichever platform they're on. Cross-platform verification (running on the other OS) can batch at end of each phase.

**Version pinning:** The exact NuGet versions below reflect the latest stable at spec time (2026-04-23). If a newer patch exists when the engineer runs this, use it. If a version doesn't resolve, check `dotnet nuget list` for the nearest stable.

**Known uncertainties:**
- `SharpHook` may need adjustment for Avalonia 12. Verify at Task 23 and fall back to platform-specific P/Invoke (Win32 `RegisterHotKey`, macOS `RegisterEventHotKey`) if needed.
- Avalonia 12 introduced breaking changes vs 11; a few APIs here (e.g. `TransparencyLevelHint`) may have renamed/moved. Engineer: check [Avalonia 12 breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes) when scaffolding and adjust names as needed. Where this happens, update the plan file inline and commit that edit as its own fix.

---

## Phase 1 — Scaffold

Builds the empty solution that compiles and tests cleanly.

---

### Task 1: Create the solution and project tree

**Files:**
- Create: `Orbital.sln`
- Create: `src/Orbital.Core/Orbital.Core.csproj`
- Create: `src/Orbital.App/Orbital.App.csproj`
- Create: `tests/Orbital.Core.Tests/Orbital.Core.Tests.csproj`

- [ ] **Step 1: Create solution file**

Run:
```bash
cd /Users/radaiko/dev/private/Orbital
dotnet new sln -n Orbital
```

Expected: `Orbital.sln` created in repo root.

- [ ] **Step 2: Create Core library project**

Run:
```bash
dotnet new classlib -n Orbital.Core -o src/Orbital.Core --framework net10.0
rm src/Orbital.Core/Class1.cs
```

Expected: `src/Orbital.Core/Orbital.Core.csproj` exists; no default `Class1.cs`.

- [ ] **Step 3: Create App project**

Avalonia 12 has its own template. Install then create:

Run:
```bash
dotnet new install Avalonia.Templates
dotnet new avalonia.app -n Orbital.App -o src/Orbital.App --framework net10.0
```

If the Avalonia template was already installed, the install command is a no-op. If the generated project does not reference Avalonia 12 in its csproj, edit the `<PackageReference Include="Avalonia" ...>` version to `12.0.1` and related `Avalonia.*` packages to the same minor.

Expected: `src/Orbital.App/` contains `App.axaml`, `App.axaml.cs`, `Program.cs`, `Orbital.App.csproj`, and a default `MainWindow.axaml`. We will delete `MainWindow*` later (Task 17).

- [ ] **Step 4: Create test project**

Run:
```bash
dotnet new xunit -n Orbital.Core.Tests -o tests/Orbital.Core.Tests --framework net10.0
rm tests/Orbital.Core.Tests/UnitTest1.cs
```

Expected: test project exists without default tests.

- [ ] **Step 5: Add test project references and FluentAssertions**

Run:
```bash
dotnet add tests/Orbital.Core.Tests/Orbital.Core.Tests.csproj reference src/Orbital.Core/Orbital.Core.csproj
dotnet add tests/Orbital.Core.Tests/Orbital.Core.Tests.csproj package FluentAssertions --version 6.12.0
```

- [ ] **Step 6: Add App → Core reference**

Run:
```bash
dotnet add src/Orbital.App/Orbital.App.csproj reference src/Orbital.Core/Orbital.Core.csproj
```

- [ ] **Step 7: Register all three projects in solution**

Run:
```bash
dotnet sln add src/Orbital.Core/Orbital.Core.csproj src/Orbital.App/Orbital.App.csproj tests/Orbital.Core.Tests/Orbital.Core.Tests.csproj
```

- [ ] **Step 8: Verify solution builds**

Run: `dotnet build`

Expected: `Build succeeded. 0 Warning(s), 0 Error(s)`.

- [ ] **Step 9: Verify tests run (empty set is fine)**

Run: `dotnet test`

Expected: `Passed!  - Failed: 0, Passed: 0, Skipped: 0` — an empty run passes.

- [ ] **Step 10: Commit**

```bash
git add Orbital.sln src/ tests/
git commit -m "Scaffold Orbital solution with Core, App, and test projects"
```

---

### Task 2: Add shared build properties and repository hygiene files

**Files:**
- Create: `Directory.Build.props`
- Create: `.editorconfig`
- Create: `.gitignore`

- [ ] **Step 1: Write `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors>CS1591</WarningsNotAsErrors>
    <AnalysisMode>Recommended</AnalysisMode>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <Company>Orbital</Company>
    <Product>Orbital</Product>
  </PropertyGroup>
</Project>
```

The existing csproj files' per-project `TargetFramework` and `Nullable` lines are now redundant but harmless; leave them for now.

- [ ] **Step 2: Write `.editorconfig`**

```ini
root = true

[*]
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
charset = utf-8

[*.{cs,axaml,xaml}]
indent_style = space
indent_size = 4

[*.{json,yml,yaml}]
indent_style = space
indent_size = 2

[*.cs]
# File-scoped namespaces
csharp_style_namespace_declarations = file_scoped:warning
# var preferences
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:silent
# Expression-bodied members
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
# Modifier order
csharp_preferred_modifier_order = public,private,protected,internal,file,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,required,volatile,async:warning
dotnet_sort_system_directives_first = true
```

- [ ] **Step 3: Write `.gitignore`**

```gitignore
# .NET build output
[Bb]in/
[Oo]bj/
[Oo]ut/
[Pp]ublish/

# Rider / VS / VS Code
.idea/
.vs/
*.user
*.suo
*.DotSettings.user
.vscode/
launchSettings.json

# NuGet
*.nupkg
.nuget/

# macOS
.DS_Store

# Orbital data (never commit user data)
**/todos.json
**/todos.json.tmp
**/todos.backup.json
**/settings.json
```

- [ ] **Step 4: Verify build still clean**

Run: `dotnet build`

Expected: Build still succeeds. If analyzer errors show up, they indicate real style issues in the generated scaffold — address them by adding `global using` where needed or fix nullability warnings. None expected.

- [ ] **Step 5: Commit**

```bash
git add Directory.Build.props .editorconfig .gitignore
git commit -m "Add shared build properties, editorconfig, and gitignore"
```

---

### Task 3: Write the project README and CLAUDE.md

**Files:**
- Modify: `README.md`
- Create: `CLAUDE.md`

- [ ] **Step 1: Replace `README.md` with real content**

Read the existing file first; it currently contains only `# Orbital`.

Write:

```markdown
# Orbital

A fast, local, keyboard-driven personal todo list for Windows and macOS.

Two global hotkeys:

- `Ctrl+Alt+T` — Quick add. A tiny popup with a title and a natural-language due date.
- `Ctrl+Alt+L` — Toggle the overlay. A translucent always-on-top list with drag-to-reorder.

No account, no cloud, no priorities. Manual order, due dates for display.

## Install

**Windows** — download the latest `orbital-win-x64.zip` from releases, unzip anywhere, double-click `Orbital.exe`. The app lives in the system tray.

**macOS** — download `Orbital-macOS-arm64.zip`, unzip, drag `Orbital.app` into `/Applications`. On first launch, right-click → Open (the app is unsigned for now). On first hotkey press macOS will prompt for **Accessibility permission** — allow it in System Settings → Privacy & Security → Accessibility, then quit and relaunch.

**Linux** (bonus; not a first-class target) — run `dotnet publish -r linux-x64 --self-contained -p:PublishSingleFile=true` from source.

## Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/<you>/Orbital.git
cd Orbital
dotnet build
dotnet test
dotnet run --project src/Orbital.App
```

## Hotkeys

| Action                | Default        |
|-----------------------|----------------|
| Quick add             | `Ctrl+Alt+T`   |
| Toggle overlay        | `Ctrl+Alt+L`   |

Both are configurable in Settings (right-click tray → Settings…).

## Due-date syntax

The quick-add due-date field accepts:

| Input           | Meaning                          |
|-----------------|----------------------------------|
| `today`         | Today                            |
| `tomorrow`, `tmr` | Tomorrow                       |
| `in 3 days`     | Three days from today            |
| `in 2 weeks`    | Two weeks from today             |
| `fri`, `friday` | Next Friday (or today if it is) |
| `next mon`      | Monday after this one            |
| `next week`     | Next Monday                      |
| `next month`    | Same day next month (clamped)    |
| `25.4`, `25/4`  | 25 April this year (or next)     |
| `2026-04-25`    | ISO date                         |

## Data location

- Windows: `%APPDATA%\Orbital\`
- macOS: `~/Library/Application Support/Orbital/`
- Linux: `$XDG_DATA_HOME/Orbital/` or `~/.local/share/Orbital/`

Files: `todos.json`, `settings.json`. Both are plain JSON — safe to back up, edit, or sync manually via any cloud drive.

## Troubleshooting

- **Hotkeys don't work on macOS** — grant Accessibility in System Settings → Privacy & Security → Accessibility, then quit and relaunch.
- **Hotkey conflict** — change the binding in Settings.
- **Want to reset everything** — Settings → Data → Reset all data (writes a backup first).

## License

TBD.
```

- [ ] **Step 2: Write `CLAUDE.md`**

```markdown
# Orbital — Notes for Claude sessions

## What this is

Local-first personal todo app for Windows + macOS. Tray-resident, two global hotkeys, JSON storage. See [design spec](docs/superpowers/specs/2026-04-23-orbital-todo-design.md) for the full picture.

## Project layout

- `src/Orbital.Core/` — pure .NET domain (models, JSON store, date parser). Testable in isolation, no UI or platform deps.
- `src/Orbital.App/` — Avalonia UI + all platform glue (hotkeys via SharpHook, tray icon, autostart).
- `tests/Orbital.Core.Tests/` — xUnit tests for the core library.

**Rule of thumb:** if it's about todos, it goes in Core. If it's about pixels, windows, hotkeys, or the OS, it goes in App.

## Conventions

- C# style: file-scoped namespaces, nullable enabled, `var` when type is apparent, 4-space indent.
- Analyzers are errors (see `Directory.Build.props`). Don't suppress — fix the underlying issue.
- Prefer `sealed` records for models, `sealed` classes for services.
- One class per file.

## Before claiming a task done

1. `dotnet build` — must pass with 0 warnings.
2. `dotnet test` — all green.
3. If UI changed: run `dotnet run --project src/Orbital.App` and verify the feature manually. This check is not optional — type checks don't catch broken XAML bindings or wrong window positioning.
4. Commit with a descriptive message.

## Platform targets

- **Windows 10/11 x64** — first-class.
- **macOS 13+ arm64** — first-class. x64 on request.
- **Linux** — best-effort; runs but not actively tested.

## Known platform quirks

- **macOS Accessibility permission** is required for SharpHook to receive key events. On denial, the app shouldn't crash — it should show a banner in the tray menu explaining how to enable.
- **macOS `LSUIElement`** — the `.app` bundle's `Info.plist` must set `LSUIElement=true` so no Dock icon shows. See `build/macos/Info.plist.template`.
- **Windows single-instance** — enforced via named mutex `Global\Orbital.SingleInstance`. On macOS we use a pidfile with `flock`.

## Packaging

Build scripts in `build/`:

- `build/publish-win.ps1` — self-contained single-file Windows exe, zipped.
- `build/publish-mac.sh` — `.app` bundle with Info.plist, zipped.
- `build/build.sh` — wrapper that picks based on current OS.

MVP ships unsigned. macOS users right-click → Open on first launch.

## Testing approach

- Core domain (parser, store, ordering): table-driven xUnit tests, high coverage.
- ViewModels: unit-testable by constructing with a stub `ITodoStore`.
- UI / platform glue: smoke-tested by hand. Don't write UI tests for MVP.

## Data files

Never commit `todos.json`, `settings.json`, or any `*.backup.json`. The `.gitignore` covers these globally.
```

- [ ] **Step 3: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "Add README and CLAUDE.md with conventions and install notes"
```

---

### Task 4: Add CommunityToolkit.Mvvm and FluentAssertions to projects

**Files:**
- Modify: `src/Orbital.App/Orbital.App.csproj`
- Modify: `src/Orbital.Core/Orbital.Core.csproj`
- Modify: `tests/Orbital.Core.Tests/Orbital.Core.Tests.csproj`

- [ ] **Step 1: Add MVVM toolkit to App**

Run:
```bash
dotnet add src/Orbital.App/Orbital.App.csproj package CommunityToolkit.Mvvm --version 8.2.2
```

- [ ] **Step 2: Add SharpHook to App (early; used later)**

Run:
```bash
dotnet add src/Orbital.App/Orbital.App.csproj package SharpHook --version 5.3.8
```

(If this package version doesn't resolve, list available versions with `dotnet package search SharpHook` and use the latest 5.x stable.)

- [ ] **Step 3: Verify solution still builds**

Run: `dotnet build`

Expected: 0 errors, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Orbital.App.csproj
git commit -m "Add CommunityToolkit.Mvvm and SharpHook packages"
```

---

## Phase 2 — Core domain

Pure C#, fully testable, no Avalonia dependency. Written TDD.

---

### Task 5: `Todo` and `AppSettings` models

**Files:**
- Create: `src/Orbital.Core/Models/Todo.cs`
- Create: `src/Orbital.Core/Models/AppSettings.cs`
- Create: `src/Orbital.Core/Models/HotkeyBinding.cs`
- Create: `src/Orbital.Core/Models/OverlayPosition.cs`
- Create: `tests/Orbital.Core.Tests/Models/TodoTests.cs`

- [ ] **Step 1: Write the failing test for `Todo` construction**

```csharp
// tests/Orbital.Core.Tests/Models/TodoTests.cs
namespace Orbital.Core.Tests.Models;

using FluentAssertions;
using Orbital.Core.Models;
using Xunit;

public sealed class TodoTests
{
    [Fact]
    public void Todo_is_constructable_with_required_fields()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var todo = new Todo
        {
            Id = id,
            Title = "Buy milk",
            CreatedAt = createdAt,
            Order = 0,
        };

        todo.Id.Should().Be(id);
        todo.Title.Should().Be("Buy milk");
        todo.CreatedAt.Should().Be(createdAt);
        todo.Order.Should().Be(0);
        todo.DueDate.Should().BeNull();
        todo.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Todo_active_vs_complete_is_computed_from_CompletedAt()
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            Title = "x",
            CreatedAt = DateTimeOffset.UtcNow,
            Order = 0,
        };
        todo.IsCompleted.Should().BeFalse();
        todo.CompletedAt = DateTimeOffset.UtcNow;
        todo.IsCompleted.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the test — expect compile failure**

Run: `dotnet test tests/Orbital.Core.Tests -v minimal`

Expected: Compile error — `Todo` type not found.

- [ ] **Step 3: Create `Todo`**

```csharp
// src/Orbital.Core/Models/Todo.cs
namespace Orbital.Core.Models;

public sealed record Todo
{
    public required Guid Id { get; init; }
    public required string Title { get; set; }
    public DateOnly? DueDate { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public required int Order { get; set; }

    public bool IsCompleted => CompletedAt is not null;
}
```

- [ ] **Step 4: Run tests — expect pass**

Run: `dotnet test tests/Orbital.Core.Tests -v minimal`

Expected: 2 passed.

- [ ] **Step 5: Add the `HotkeyBinding` record and `OverlayPosition` enum**

```csharp
// src/Orbital.Core/Models/OverlayPosition.cs
namespace Orbital.Core.Models;

public enum OverlayPosition
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft,
}
```

```csharp
// src/Orbital.Core/Models/HotkeyBinding.cs
namespace Orbital.Core.Models;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
    Meta = 8, // Cmd on macOS, Windows key on Windows
}

public sealed record HotkeyBinding(HotkeyModifiers Modifiers, string KeyName)
{
    public static HotkeyBinding Default(string keyName) =>
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, keyName);
}
```

We use a string `KeyName` (e.g. `"T"`, `"L"`, `"F6"`) rather than a concrete `Avalonia.Input.Key` so `Orbital.Core` stays UI-framework-free. The App layer translates to and from Avalonia's types.

- [ ] **Step 6: Add `AppSettings`**

```csharp
// src/Orbital.Core/Models/AppSettings.cs
namespace Orbital.Core.Models;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public HotkeyBinding QuickAddHotkey { get; set; } = HotkeyBinding.Default("T");
    public HotkeyBinding ToggleOverlayHotkey { get; set; } = HotkeyBinding.Default("L");
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopRight;
    public bool OverlayAutoHideOnFocusLoss { get; set; } = true;
    public bool ShowCompleted { get; set; } = false;
    public bool StartAtLogin { get; set; } = false;
}
```

- [ ] **Step 7: Run build and tests**

Run: `dotnet build && dotnet test`

Expected: 0 errors, 2 passed.

- [ ] **Step 8: Commit**

```bash
git add src/Orbital.Core/Models/ tests/Orbital.Core.Tests/Models/
git commit -m "Add Todo, AppSettings, HotkeyBinding models with tests"
```

---

### Task 6: `AppPaths` — per-OS data directory resolution

**Files:**
- Create: `src/Orbital.Core/Persistence/AppPaths.cs`
- Create: `tests/Orbital.Core.Tests/Persistence/AppPathsTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
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
```

- [ ] **Step 2: Verify tests fail (missing type)**

Run: `dotnet test`

Expected: Compile error — `AppPaths` not found.

- [ ] **Step 3: Implement `AppPaths`**

```csharp
// src/Orbital.Core/Persistence/AppPaths.cs
namespace Orbital.Core.Persistence;

using System.Runtime.InteropServices;

public static class AppPaths
{
    private const string AppDirName = "Orbital";

    public static string DataDirectory { get; } = ResolveDataDirectory();
    public static string TodosFile { get; } = Path.Combine(DataDirectory, "todos.json");
    public static string SettingsFile { get; } = Path.Combine(DataDirectory, "settings.json");
    public static string TodosBackupFile { get; } = Path.Combine(DataDirectory, "todos.backup.json");

    private static string ResolveDataDirectory()
    {
        string baseDir;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // %APPDATA% = C:\Users\<u>\AppData\Roaming
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            baseDir = Path.Combine(home, "Library", "Application Support");
        }
        else
        {
            // Linux / BSD / other: XDG_DATA_HOME or ~/.local/share
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            baseDir = !string.IsNullOrEmpty(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
        return Path.Combine(baseDir, AppDirName);
    }

    public static void EnsureDataDirectoryExists()
    {
        Directory.CreateDirectory(DataDirectory);
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

Run: `dotnet test`

Expected: 5 passed (2 from before + 3 new).

- [ ] **Step 5: Commit**

```bash
git add src/Orbital.Core/Persistence/ tests/Orbital.Core.Tests/Persistence/
git commit -m "Add AppPaths with per-OS data directory resolution"
```

---

### Task 7: `DueDateParser` — core behaviors

**Files:**
- Create: `src/Orbital.Core/DateParsing/ParseResult.cs`
- Create: `src/Orbital.Core/DateParsing/DueDateParser.cs`
- Create: `tests/Orbital.Core.Tests/DateParsing/DueDateParserTests.cs`

This is the most test-worthy piece in the codebase. Table-driven tests cover every grammar rule.

- [ ] **Step 1: Write `ParseResult` type first (no tests — trivial)**

```csharp
// src/Orbital.Core/DateParsing/ParseResult.cs
namespace Orbital.Core.DateParsing;

public readonly record struct ParseResult
{
    public bool IsEmpty { get; init; }
    public bool IsError { get; init; }
    public DateOnly? Date { get; init; }
    public string? ErrorMessage { get; init; }

    public static ParseResult Empty() => new() { IsEmpty = true };
    public static ParseResult Ok(DateOnly d) => new() { Date = d };
    public static ParseResult Error(string msg) => new() { IsError = true, ErrorMessage = msg };
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// tests/Orbital.Core.Tests/DateParsing/DueDateParserTests.cs
namespace Orbital.Core.Tests.DateParsing;

using FluentAssertions;
using Orbital.Core.DateParsing;
using Xunit;

public sealed class DueDateParserTests
{
    // Fixed "today" for deterministic tests — a Thursday so weekday rollover is visible.
    private static readonly DateOnly Today = new(2026, 4, 23); // 2026-04-23 is a Thursday
    private readonly DueDateParser parser = new(() => Today);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_returns_Empty(string input)
    {
        var r = parser.Parse(input);
        r.IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData("today",    2026, 4, 23)]
    [InlineData("tomorrow", 2026, 4, 24)]
    [InlineData("tmr",      2026, 4, 24)]
    [InlineData("TODAY",    2026, 4, 23)] // case-insensitive
    public void Relative_literals(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("in 1 day",   2026, 4, 24)]
    [InlineData("in 3 days",  2026, 4, 26)]
    [InlineData("in 1 week",  2026, 4, 30)]
    [InlineData("in 2 weeks", 2026, 5, 7)]
    public void In_N_days_and_weeks(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("mon",     2026, 4, 27)] // next Monday (today is Thu)
    [InlineData("monday",  2026, 4, 27)]
    [InlineData("thu",     2026, 4, 23)] // today
    [InlineData("fri",     2026, 4, 24)]
    [InlineData("sunday",  2026, 4, 26)]
    public void Weekday_resolves_to_next_occurrence(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("next mon",   2026, 5, 4)]   // Monday after this one
    [InlineData("next week",  2026, 4, 27)]  // next Monday
    [InlineData("next month", 2026, 5, 23)]  // same day next month
    public void Next_X(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("2026-04-25", 2026, 4, 25)]
    [InlineData("2026/04/25", 2026, 4, 25)]
    [InlineData("2026-4-25",  2026, 4, 25)]  // permissive single-digit month/day
    public void Iso_dates(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("25.4",   2026, 4, 25)]   // future this year
    [InlineData("25/4",   2026, 4, 25)]
    [InlineData("25-04",  2026, 4, 25)]
    [InlineData("1.1",    2027, 1, 1)]    // already past → next year
    [InlineData("22.4",   2027, 4, 22)]   // 22 Apr 2026 is past → next year
    public void Short_day_month(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("asdf")]
    [InlineData("99.99")]
    [InlineData("in 0 days")]
    [InlineData("in -1 days")]
    [InlineData("2026-13-01")]
    [InlineData("next yesterday")]
    public void Invalid_inputs_return_error(string input)
    {
        var r = parser.Parse(input);
        r.IsError.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Verify tests fail (compile error — `DueDateParser` missing)**

Run: `dotnet test`

Expected: Compile error.

- [ ] **Step 4: Implement `DueDateParser`**

```csharp
// src/Orbital.Core/DateParsing/DueDateParser.cs
namespace Orbital.Core.DateParsing;

using System.Globalization;
using System.Text.RegularExpressions;

public sealed class DueDateParser
{
    private readonly Func<DateOnly> today;

    public DueDateParser() : this(() => DateOnly.FromDateTime(DateTime.Today)) { }

    public DueDateParser(Func<DateOnly> todayProvider)
    {
        today = todayProvider;
    }

    public ParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ParseResult.Empty();

        var s = input.Trim().ToLowerInvariant();

        // Rule 1: relative literals
        switch (s)
        {
            case "today": return ParseResult.Ok(today());
            case "tomorrow":
            case "tmr":
                return ParseResult.Ok(today().AddDays(1));
        }

        // Rule 2: "in N days" / "in N weeks"
        var inMatch = Regex.Match(s, @"^in\s+(-?\d+)\s+(day|days|week|weeks)$");
        if (inMatch.Success)
        {
            var n = int.Parse(inMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (n <= 0) return ParseResult.Error("must be positive");
            var unit = inMatch.Groups[2].Value;
            var days = unit.StartsWith("week") ? n * 7 : n;
            return ParseResult.Ok(today().AddDays(days));
        }

        // Rule 3: ISO date — yyyy-m-d or yyyy/m/d
        var isoMatch = Regex.Match(s, @"^(\d{4})[-/](\d{1,2})[-/](\d{1,2})$");
        if (isoMatch.Success)
        {
            if (TryBuildDate(int.Parse(isoMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                             int.Parse(isoMatch.Groups[2].Value, CultureInfo.InvariantCulture),
                             int.Parse(isoMatch.Groups[3].Value, CultureInfo.InvariantCulture),
                             out var iso))
                return ParseResult.Ok(iso);
            return ParseResult.Error("invalid ISO date");
        }

        // Rule 4: "next ..."
        if (s.StartsWith("next "))
        {
            var rest = s[5..].Trim();
            if (rest == "week")
                return ParseResult.Ok(NextWeekday(DayOfWeek.Monday, skipIfToday: true));
            if (rest == "month")
                return ParseResult.Ok(AddMonthsClamped(today(), 1));
            if (TryParseWeekday(rest, out var dow))
                return ParseResult.Ok(NextWeekday(dow, skipIfToday: true).AddDays(7).AddDays(-0));
            return ParseResult.Error($"don't understand 'next {rest}'");
        }

        // Rule 5: weekday
        if (TryParseWeekday(s, out var wd))
            return ParseResult.Ok(NextWeekday(wd, skipIfToday: false));

        // Rule 6: short day.month with . / -
        var shortMatch = Regex.Match(s, @"^(\d{1,2})[./-](\d{1,2})$");
        if (shortMatch.Success)
        {
            var day = int.Parse(shortMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(shortMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            var t = today();
            if (TryBuildDate(t.Year, month, day, out var thisYear))
            {
                if (thisYear < t) return ParseResult.Ok(new DateOnly(t.Year + 1, month, day));
                return ParseResult.Ok(thisYear);
            }
            return ParseResult.Error("invalid date");
        }

        return ParseResult.Error("unrecognized");
    }

    // Helpers --------------------------------------------------------------

    private static bool TryBuildDate(int year, int month, int day, out DateOnly date)
    {
        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }

    private DateOnly NextWeekday(DayOfWeek target, bool skipIfToday)
    {
        var t = today();
        var diff = ((int)target - (int)t.DayOfWeek + 7) % 7;
        if (diff == 0 && skipIfToday) diff = 7;
        return t.AddDays(diff);
    }

    private static DateOnly AddMonthsClamped(DateOnly d, int months)
    {
        var year = d.Year + (d.Month - 1 + months) / 12;
        var month = (d.Month - 1 + months) % 12 + 1;
        var lastDay = DateTime.DaysInMonth(year, month);
        var day = Math.Min(d.Day, lastDay);
        return new DateOnly(year, month, day);
    }

    private static bool TryParseWeekday(string input, out DayOfWeek day)
    {
        day = input switch
        {
            "mon" or "monday" => DayOfWeek.Monday,
            "tue" or "tues" or "tuesday" => DayOfWeek.Tuesday,
            "wed" or "wednesday" => DayOfWeek.Wednesday,
            "thu" or "thur" or "thurs" or "thursday" => DayOfWeek.Thursday,
            "fri" or "friday" => DayOfWeek.Friday,
            "sat" or "saturday" => DayOfWeek.Saturday,
            "sun" or "sunday" => DayOfWeek.Sunday,
            _ => (DayOfWeek)(-1),
        };
        return (int)day >= 0;
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test`

Expected: all parser tests pass. If a case fails, the table row and expected value in the test pinpoint the rule — fix the implementation rule (not the test) unless the test has a typo.

- [ ] **Step 6: Commit**

```bash
git add src/Orbital.Core/DateParsing/ tests/Orbital.Core.Tests/DateParsing/
git commit -m "Add DueDateParser with table-driven tests for all grammar rules"
```

---

### Task 8: `JsonTodoStore` with atomic write

**Files:**
- Create: `src/Orbital.Core/Persistence/ITodoStore.cs`
- Create: `src/Orbital.Core/Persistence/JsonTodoStore.cs`
- Create: `src/Orbital.Core/Persistence/TodosFileDto.cs`
- Create: `tests/Orbital.Core.Tests/Persistence/JsonTodoStoreTests.cs`

- [ ] **Step 1: Define interface**

```csharp
// src/Orbital.Core/Persistence/ITodoStore.cs
namespace Orbital.Core.Persistence;

using Orbital.Core.Models;

public interface ITodoStore
{
    Task<IReadOnlyList<Todo>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IReadOnlyList<Todo> todos, CancellationToken ct = default);
}
```

- [ ] **Step 2: Define the on-disk DTO**

We separate the wire format from the domain model so we can evolve the schema later without touching `Todo`.

```csharp
// src/Orbital.Core/Persistence/TodosFileDto.cs
namespace Orbital.Core.Persistence;

using System.Text.Json.Serialization;
using Orbital.Core.Models;

internal sealed record TodosFileDto
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("todos")]         public List<TodoDto> Todos { get; init; } = new();
}

internal sealed record TodoDto
{
    [JsonPropertyName("id")]          public Guid Id { get; init; }
    [JsonPropertyName("title")]       public string Title { get; init; } = "";
    [JsonPropertyName("dueDate")]     public DateOnly? DueDate { get; init; }
    [JsonPropertyName("createdAt")]   public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("completedAt")] public DateTimeOffset? CompletedAt { get; init; }
    [JsonPropertyName("order")]       public int Order { get; init; }

    public static TodoDto From(Todo t) => new()
    {
        Id = t.Id, Title = t.Title, DueDate = t.DueDate,
        CreatedAt = t.CreatedAt, CompletedAt = t.CompletedAt, Order = t.Order,
    };

    public Todo ToDomain() => new()
    {
        Id = Id, Title = Title, DueDate = DueDate,
        CreatedAt = CreatedAt, CompletedAt = CompletedAt, Order = Order,
    };
}
```

- [ ] **Step 3: Write failing tests**

```csharp
// tests/Orbital.Core.Tests/Persistence/JsonTodoStoreTests.cs
namespace Orbital.Core.Tests.Persistence;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.Persistence;
using Xunit;

public sealed class JsonTodoStoreTests : IDisposable
{
    private readonly string tempDir;
    private readonly string todosFile;
    private readonly JsonTodoStore store;

    public JsonTodoStoreTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "orbital-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        todosFile = Path.Combine(tempDir, "todos.json");
        store = new JsonTodoStore(todosFile);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public async Task Load_returns_empty_when_file_does_not_exist()
    {
        var todos = await store.LoadAsync();
        todos.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_then_load_round_trips()
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            DueDate = new DateOnly(2026, 4, 24),
            CreatedAt = DateTimeOffset.Parse("2026-04-23T09:12:00+00:00"),
            Order = 0,
        };
        await store.SaveAsync(new[] { todo });
        var loaded = await store.LoadAsync();
        loaded.Should().HaveCount(1);
        loaded[0].Should().BeEquivalentTo(todo);
    }

    [Fact]
    public async Task Save_is_atomic_leaves_no_tmp_file_on_success()
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(), Title = "x", CreatedAt = DateTimeOffset.UtcNow, Order = 0,
        };
        await store.SaveAsync(new[] { todo });
        File.Exists(todosFile).Should().BeTrue();
        File.Exists(todosFile + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task Load_is_tolerant_of_empty_file()
    {
        await File.WriteAllTextAsync(todosFile, "");
        var loaded = await store.LoadAsync();
        loaded.Should().BeEmpty();
    }

    [Fact]
    public async Task Load_throws_on_corrupt_json()
    {
        await File.WriteAllTextAsync(todosFile, "{not json");
        Func<Task> act = async () => await store.LoadAsync();
        await act.Should().ThrowAsync<Exception>();
    }
}
```

- [ ] **Step 4: Verify tests fail (JsonTodoStore not defined)**

Run: `dotnet test`

Expected: Compile error.

- [ ] **Step 5: Implement `JsonTodoStore`**

```csharp
// src/Orbital.Core/Persistence/JsonTodoStore.cs
namespace Orbital.Core.Persistence;

using System.Text.Json;
using Orbital.Core.Models;

public sealed class JsonTodoStore : ITodoStore
{
    private readonly string filePath;
    private readonly JsonSerializerOptions options;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public JsonTodoStore(string? filePath = null)
    {
        this.filePath = filePath ?? AppPaths.TodosFile;
        options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public async Task<IReadOnlyList<Todo>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return Array.Empty<Todo>();

        var json = await File.ReadAllTextAsync(filePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<Todo>();

        var dto = JsonSerializer.Deserialize<TodosFileDto>(json, options)
                  ?? throw new InvalidDataException("todos.json deserialized to null");
        return dto.Todos.Select(d => d.ToDomain()).ToArray();
    }

    public async Task SaveAsync(IReadOnlyList<Todo> todos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(todos);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var dto = new TodosFileDto
        {
            SchemaVersion = 1,
            Todos = todos.Select(TodoDto.From).ToList(),
        };

        await writeLock.WaitAsync(ct);
        try
        {
            var tmpPath = filePath + ".tmp";
            await using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, dto, options, ct);
                await fs.FlushAsync(ct);
                fs.Flush(flushToDisk: true);
            }
            File.Move(tmpPath, filePath, overwrite: true);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test`

Expected: all JsonTodoStore tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Orbital.Core/Persistence/ tests/Orbital.Core.Tests/Persistence/
git commit -m "Add JsonTodoStore with atomic writes and tests"
```

---

### Task 9: `JsonSettingsStore`

**Files:**
- Create: `src/Orbital.Core/Persistence/ISettingsStore.cs`
- Create: `src/Orbital.Core/Persistence/JsonSettingsStore.cs`
- Create: `tests/Orbital.Core.Tests/Persistence/JsonSettingsStoreTests.cs`

- [ ] **Step 1: Define interface**

```csharp
// src/Orbital.Core/Persistence/ISettingsStore.cs
namespace Orbital.Core.Persistence;

using Orbital.Core.Models;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write failing tests**

```csharp
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
```

- [ ] **Step 3: Implement**

```csharp
// src/Orbital.Core/Persistence/JsonSettingsStore.cs
namespace Orbital.Core.Persistence;

using System.Text.Json;
using Orbital.Core.Models;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string filePath;
    private readonly JsonSerializerOptions options;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public JsonSettingsStore(string? filePath = null)
    {
        this.filePath = filePath ?? AppPaths.SettingsFile;
        options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return new AppSettings();
        var json = await File.ReadAllTextAsync(filePath, ct);
        if (string.IsNullOrWhiteSpace(json)) return new AppSettings();
        return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await writeLock.WaitAsync(ct);
        try
        {
            var tmp = filePath + ".tmp";
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, settings, options, ct);
                await fs.FlushAsync(ct);
                fs.Flush(flushToDisk: true);
            }
            File.Move(tmp, filePath, overwrite: true);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
```

- [ ] **Step 4: Run tests — expect all pass**

Run: `dotnet test`

- [ ] **Step 5: Commit**

```bash
git add src/Orbital.Core/Persistence/ tests/Orbital.Core.Tests/Persistence/
git commit -m "Add JsonSettingsStore with defaults and round-trip tests"
```

---

### Task 10: Ordering helpers

**Files:**
- Create: `src/Orbital.Core/Ordering/TodoOrdering.cs`
- Create: `tests/Orbital.Core.Tests/Ordering/TodoOrderingTests.cs`

The overlay and quick-add both need consistent logic for computing the `Order` of a new todo and for re-sequencing after a drag. Centralising this avoids drift.

- [ ] **Step 1: Tests**

```csharp
// tests/Orbital.Core.Tests/Ordering/TodoOrderingTests.cs
namespace Orbital.Core.Tests.Ordering;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.Ordering;
using Xunit;

public sealed class TodoOrderingTests
{
    private static Todo Make(int order) => new()
    {
        Id = Guid.NewGuid(),
        Title = $"t{order}",
        CreatedAt = DateTimeOffset.UtcNow,
        Order = order,
    };

    [Fact]
    public void NextTopOrder_returns_zero_when_list_empty()
    {
        TodoOrdering.NextTopOrder(Array.Empty<Todo>()).Should().Be(0);
    }

    [Fact]
    public void NextTopOrder_returns_min_minus_one()
    {
        var todos = new[] { Make(5), Make(-2), Make(0) };
        TodoOrdering.NextTopOrder(todos).Should().Be(-3);
    }

    [Fact]
    public void ReorderActive_moves_item_to_target_index()
    {
        var a = Make(0);
        var b = Make(1);
        var c = Make(2);
        var d = Make(3);
        var list = new List<Todo> { a, b, c, d };

        TodoOrdering.ReorderActive(list, fromIndex: 0, toIndex: 2);

        list.Select(t => t.Title).Should().Equal("t1", "t2", "t0", "t3");
        list.Select(t => t.Order).Should().BeInAscendingOrder();
    }

    [Fact]
    public void ReorderActive_is_noop_when_from_equals_to()
    {
        var list = new List<Todo> { Make(0), Make(1) };
        TodoOrdering.ReorderActive(list, 1, 1);
        list.Select(t => t.Title).Should().Equal("t0", "t1");
    }
}
```

- [ ] **Step 2: Implement**

```csharp
// src/Orbital.Core/Ordering/TodoOrdering.cs
namespace Orbital.Core.Ordering;

using Orbital.Core.Models;

public static class TodoOrdering
{
    public static int NextTopOrder(IReadOnlyCollection<Todo> existing)
    {
        if (existing.Count == 0) return 0;
        return existing.Min(t => t.Order) - 1;
    }

    public static void ReorderActive(List<Todo> items, int fromIndex, int toIndex)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (fromIndex == toIndex) return;
        var item = items[fromIndex];
        items.RemoveAt(fromIndex);
        items.Insert(toIndex, item);
        // Reassign sequential orders so drag history doesn't accumulate gaps that confuse later sorts.
        for (int i = 0; i < items.Count; i++)
            items[i].Order = i;
    }
}
```

- [ ] **Step 3: Run tests; commit**

```bash
dotnet test
git add src/Orbital.Core/Ordering/ tests/Orbital.Core.Tests/Ordering/
git commit -m "Add TodoOrdering helpers with reorder + top-order tests"
```

---

## Phase 3 — App shell

Minimal Avalonia host: tray-resident, no main window, single-instance, empty hotkey pipeline.

---

### Task 11: Replace the default `MainWindow` with a headless app entry

**Files:**
- Delete: `src/Orbital.App/MainWindow.axaml`
- Delete: `src/Orbital.App/MainWindow.axaml.cs`
- Modify: `src/Orbital.App/App.axaml.cs`
- Modify: `src/Orbital.App/App.axaml`

- [ ] **Step 1: Remove MainWindow files**

```bash
rm src/Orbital.App/MainWindow.axaml src/Orbital.App/MainWindow.axaml.cs
```

- [ ] **Step 2: Update `App.axaml.cs` to not open a main window**

```csharp
// src/Orbital.App/App.axaml.cs
namespace Orbital.App;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Intentionally no MainWindow — the tray icon is the entire app surface.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 3: Clean `App.axaml`**

```xml
<!-- src/Orbital.App/App.axaml -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Orbital.App.App"
             RequestedThemeVariant="Default">
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

- [ ] **Step 4: Ensure `Program.cs` matches template shape**

Open `src/Orbital.App/Program.cs` and confirm it reads (roughly):

```csharp
namespace Orbital.App;

using Avalonia;

internal sealed class Program
{
    [System.STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

If the template generated something different, overwrite with the above.

- [ ] **Step 5: Build, run briefly to confirm the app launches with no window**

Run: `dotnet run --project src/Orbital.App`

Expected: the process starts, no window appears. Kill with Ctrl+C — the process will not exit cleanly yet because we haven't wired an exit path. That's fine for this task; we'll address on Task 12.

- [ ] **Step 6: Commit**

```bash
git add -A src/Orbital.App/
git commit -m "Remove default MainWindow; app starts headless with no window"
```

---

### Task 12: Tray icon with Quit menu

**Files:**
- Create: `src/Orbital.App/Services/TrayIconController.cs`
- Create: `src/Orbital.App/Assets/tray-icon.ico` (placeholder — see step)
- Create: `src/Orbital.App/Assets/tray-icon.png` (placeholder)
- Modify: `src/Orbital.App/Orbital.App.csproj`
- Modify: `src/Orbital.App/App.axaml.cs`

- [ ] **Step 1: Add a placeholder tray icon**

For MVP we can use a solid colored square. Drop any 16x16 or 32x32 PNG into `src/Orbital.App/Assets/tray-icon.png`. A simple way:

```bash
mkdir -p src/Orbital.App/Assets
# create 32x32 solid PNG using a tiny base64'd PNG — or just draw one
cat > /tmp/make-icon.py <<'PY'
import struct, zlib, pathlib
def png(rgba, w, h):
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d))
    sig = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    raw = b"".join(b"\x00" + bytes(rgba) * w for _ in range(h))
    idat = zlib.compress(raw, 9)
    return sig + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")
pathlib.Path("src/Orbital.App/Assets/tray-icon.png").write_bytes(png([74,144,226,255], 32, 32))
PY
python3 /tmp/make-icon.py
```

(Or just drop any 32x32 PNG you like at that path.)

- [ ] **Step 2: Embed the asset in the App csproj**

Open `src/Orbital.App/Orbital.App.csproj` and add inside `<Project>`:

```xml
<ItemGroup>
  <AvaloniaResource Include="Assets\**" />
</ItemGroup>
```

- [ ] **Step 3: Write `TrayIconController`**

```csharp
// src/Orbital.App/Services/TrayIconController.cs
namespace Orbital.App.Services;

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

public sealed class TrayIconController : IDisposable
{
    private TrayIcon? trayIcon;

    public event Action? QuickAddRequested;
    public event Action? ToggleOverlayRequested;
    public event Action? SettingsRequested;
    public event Action? OpenDataFolderRequested;
    public event Action? QuitRequested;

    public void Show()
    {
        if (trayIcon is not null) return;

        trayIcon = new TrayIcon
        {
            ToolTipText = "Orbital",
            Icon = LoadIcon(),
            IsVisible = true,
        };
        trayIcon.Menu = BuildMenu();

        TrayIcon.SetIcons(Application.Current!, new TrayIcons { trayIcon });
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        var quickAdd = new NativeMenuItem("Quick add");
        quickAdd.Click += (_, _) => QuickAddRequested?.Invoke();
        menu.Add(quickAdd);

        var overlay = new NativeMenuItem("Show overlay");
        overlay.Click += (_, _) => ToggleOverlayRequested?.Invoke();
        menu.Add(overlay);

        menu.Add(new NativeMenuItemSeparator());

        var settings = new NativeMenuItem("Settings…");
        settings.Click += (_, _) => SettingsRequested?.Invoke();
        menu.Add(settings);

        var openFolder = new NativeMenuItem("Open data folder");
        openFolder.Click += (_, _) => OpenDataFolderRequested?.Invoke();
        menu.Add(openFolder);

        menu.Add(new NativeMenuItemSeparator());

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => QuitRequested?.Invoke();
        menu.Add(quit);

        return menu;
    }

    private static WindowIcon LoadIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Orbital.App/Assets/tray-icon.png"));
        return new WindowIcon(new Bitmap(stream));
    }

    public void Dispose()
    {
        if (trayIcon is not null)
        {
            trayIcon.IsVisible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }
    }
}
```

**Note:** `TrayIcon.SetIcons` is the Avalonia API as of 11.x. In Avalonia 12 it may have moved; if the compiler complains, find the equivalent in the 12 API (likely still `TrayIcon.SetIcons` or an `Application.TrayIcons` property) and adjust.

- [ ] **Step 4: Wire `TrayIconController` from `App.axaml.cs`**

```csharp
// src/Orbital.App/App.axaml.cs  (replace contents)
namespace Orbital.App;

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Orbital.App.Services;
using Orbital.Core.Persistence;

public partial class App : Application
{
    private TrayIconController? tray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            tray = new TrayIconController();
            tray.QuitRequested += () =>
            {
                tray?.Dispose();
                desktop.Shutdown();
            };
            tray.OpenDataFolderRequested += OpenDataFolder;
            tray.Show();

            AppPaths.EnsureDataDirectoryExists();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.DataDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open data folder: {ex}");
        }
    }
}
```

- [ ] **Step 5: Run and manually verify the tray icon appears**

Run: `dotnet run --project src/Orbital.App`

Expected (macOS): a small blue square appears in the menu bar (top-right of screen). Clicking it shows the menu. Clicking **Quit** exits cleanly.

Expected (Windows): the icon appears in the system tray (possibly in the hidden overflow area). Right-click → Quit exits.

If the icon doesn't appear, check: (a) the icon PNG is embedded as `AvaloniaResource`, (b) `TrayIcon.SetIcons` is the right API for Avalonia 12 — consult the 12 breaking-changes doc.

- [ ] **Step 6: Commit**

```bash
git add src/Orbital.App/Services/TrayIconController.cs \
        src/Orbital.App/Assets/ \
        src/Orbital.App/Orbital.App.csproj \
        src/Orbital.App/App.axaml.cs
git commit -m "Add tray icon with quit, quick-add, overlay, settings menu entries"
```

---

### Task 13: Single-instance guard

**Files:**
- Create: `src/Orbital.App/Services/SingleInstanceGuard.cs`
- Modify: `src/Orbital.App/Program.cs`

- [ ] **Step 1: Implement the guard**

```csharp
// src/Orbital.App/Services/SingleInstanceGuard.cs
namespace Orbital.App.Services;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Global\Orbital.SingleInstance";

    private Mutex? mutex;
    private FileStream? pidFile;

    public bool TryAcquire()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                mutex = null;
                return false;
            }
            return true;
        }

        // macOS/Linux: pidfile with exclusive lock.
        var dir = Orbital.Core.Persistence.AppPaths.DataDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, ".orbital.pid");
        try
        {
            pidFile = new FileStream(
                path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            pidFile.SetLength(0);
            using var writer = new StreamWriter(pidFile, leaveOpen: true);
            writer.Write(Environment.ProcessId);
            writer.Flush();
            return true;
        }
        catch (IOException)
        {
            pidFile?.Dispose();
            pidFile = null;
            return false;
        }
    }

    public void Dispose()
    {
        mutex?.ReleaseMutex();
        mutex?.Dispose();
        pidFile?.Dispose();
    }
}
```

- [ ] **Step 2: Use it from `Program.cs`**

```csharp
// src/Orbital.App/Program.cs
namespace Orbital.App;

using Avalonia;
using Orbital.App.Services;

internal sealed class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        using var guard = new SingleInstanceGuard();
        if (!guard.TryAcquire())
        {
            // Another instance is already running; exit silently.
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

- [ ] **Step 3: Manual check — launch twice, expect second to no-op**

```bash
dotnet run --project src/Orbital.App &
sleep 2
dotnet run --project src/Orbital.App
# The second run should exit immediately without a second tray icon.
```

Stop the first instance via its tray Quit, then verify the pidfile is cleaned up:
```bash
ls ~/Library/Application\ Support/Orbital/.orbital.pid 2>/dev/null && echo "pidfile still present" || echo "pidfile cleaned up"
```

Expected: after clean Quit, the pid file is gone (released by `FileStream.Dispose`). After a crash (`kill -9`), the file may linger but the next launch can still acquire the lock because the previous process holds no lock.

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Services/SingleInstanceGuard.cs src/Orbital.App/Program.cs
git commit -m "Add single-instance guard using named mutex / pidfile"
```

---

### Task 14: App-level state container

**Files:**
- Create: `src/Orbital.App/AppHost.cs`
- Modify: `src/Orbital.App/App.axaml.cs`

We need a single place that holds the loaded todos list, the settings, and exposes change notifications to all view-models. Keeping this out of `App.axaml.cs` makes it testable if we ever want to.

- [ ] **Step 1: Write `AppHost`**

```csharp
// src/Orbital.App/AppHost.cs
namespace Orbital.App;

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Orbital.Core.Models;
using Orbital.Core.Persistence;

public sealed class AppHost
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
}
```

Note: debounce implementation uses a simple approach — it's not perfect (concurrent calls create multiple Tasks) but the last write always wins because `JsonTodoStore.SaveAsync` is serialized internally by `writeLock`. A more sophisticated debouncer is overkill for this app.

- [ ] **Step 2: Instantiate `AppHost` from `App.axaml.cs`**

Modify `OnFrameworkInitializationCompleted` in `App.axaml.cs`:

```csharp
public override async void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
        AppPaths.EnsureDataDirectoryExists();

        Host = new AppHost();
        await Host.LoadAsync();

        tray = new TrayIconController();
        tray.QuitRequested += async () =>
        {
            await Host.FlushAsync();
            tray?.Dispose();
            desktop.Shutdown();
        };
        tray.OpenDataFolderRequested += OpenDataFolder;
        tray.Show();
    }
    base.OnFrameworkInitializationCompleted();
}

public AppHost? Host { get; private set; }
```

Also add at the top of the class: `private TrayIconController? tray;` (already there from Task 12).

- [ ] **Step 3: Build and run; verify clean exit saves**

```bash
dotnet run --project src/Orbital.App
# Quit via tray
ls ~/Library/Application\ Support/Orbital/todos.json && cat ~/Library/Application\ Support/Orbital/todos.json
```

Expected: `todos.json` exists with an empty todo list:
```json
{
  "schemaVersion": 1,
  "todos": []
}
```

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/AppHost.cs src/Orbital.App/App.axaml.cs
git commit -m "Add AppHost with Todos collection, settings, and debounced save"
```

---

## Phase 4 — Hotkey service

---

### Task 15: `IGlobalHotkeyService` and SharpHook-backed implementation

**Files:**
- Create: `src/Orbital.App/Services/IGlobalHotkeyService.cs`
- Create: `src/Orbital.App/Services/SharpHookGlobalHotkeyService.cs`
- Modify: `src/Orbital.App/App.axaml.cs`

- [ ] **Step 1: Interface**

```csharp
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
```

- [ ] **Step 2: SharpHook implementation**

```csharp
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
        if ((mask & (ModifierMask.LeftCtrl | ModifierMask.RightCtrl)) != 0)  mods |= HotkeyModifiers.Control;
        if ((mask & (ModifierMask.LeftShift | ModifierMask.RightShift)) != 0) mods |= HotkeyModifiers.Shift;
        if ((mask & (ModifierMask.LeftAlt | ModifierMask.RightAlt)) != 0)    mods |= HotkeyModifiers.Alt;
        if ((mask & (ModifierMask.LeftMeta | ModifierMask.RightMeta)) != 0)  mods |= HotkeyModifiers.Meta;
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

    public async ValueTask DisposeAsync()
    {
        hook.KeyPressed -= OnKeyPressed;
        hook.Dispose();
        await Task.CompletedTask;
    }

    private sealed class UnregisterHandle(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
```

Note: SharpHook's exact enum names (`ModifierMask`, `KeyCode`, `VcA` etc.) reflect 5.x. If the engineer finds the enum names differ in the installed SharpHook version, adapt (IDE autocomplete will confirm). The overall structure (TaskPoolGlobalHook, KeyPressed event, RawEvent.Mask) is stable across 5.x.

- [ ] **Step 3: Wire into `App.axaml.cs`**

Add field: `private IGlobalHotkeyService? hotkeys;`

In `OnFrameworkInitializationCompleted` (after `Host.LoadAsync`):

```csharp
hotkeys = new SharpHookGlobalHotkeyService();
_ = hotkeys.StartAsync();

hotkeys.Register(Host.Settings.QuickAddHotkey, () =>
{
    // Temporary: just beep via Debug so we can confirm registration works
    System.Diagnostics.Debug.WriteLine("Quick-add hotkey pressed");
});
hotkeys.Register(Host.Settings.ToggleOverlayHotkey, () =>
{
    System.Diagnostics.Debug.WriteLine("Overlay hotkey pressed");
});
```

Adjust `QuitRequested` handler to also dispose hotkeys:

```csharp
tray.QuitRequested += async () =>
{
    await Host!.FlushAsync();
    if (hotkeys is not null) await hotkeys.DisposeAsync();
    tray?.Dispose();
    desktop.Shutdown();
};
```

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Orbital.App`

On macOS, the first time the app runs it will try to install the global hook and the OS will prompt for **Accessibility permission** (or silently fail if denied). Grant it in System Settings → Privacy & Security → Accessibility, then **quit the app and relaunch**.

Press `Ctrl+Alt+T`. Check the Debug output (if running under a debugger) or the console for "Quick-add hotkey pressed". Same for `Ctrl+Alt+L`.

If nothing happens:
1. Confirm Accessibility permission.
2. Add a temporary log in `OnKeyPressed` *before* the modifier check to confirm key events are flowing at all.
3. Verify the `ModifierMask` enum names match the SharpHook version — autocomplete in your IDE will show them.

- [ ] **Step 5: Commit**

```bash
git add src/Orbital.App/Services/IGlobalHotkeyService.cs \
        src/Orbital.App/Services/SharpHookGlobalHotkeyService.cs \
        src/Orbital.App/App.axaml.cs
git commit -m "Add SharpHook-backed global hotkey service; wire default bindings"
```

---

## Phase 5 — Quick-add window

---

### Task 16: `QuickAddViewModel`

**Files:**
- Create: `src/Orbital.App/ViewModels/QuickAddViewModel.cs`
- Create: `tests/Orbital.Core.Tests/ViewModels/QuickAddViewModelTests.cs` (tests we lift into the test project)

Note: the VM doesn't directly depend on Avalonia — keeping it a plain `ObservableObject` from CommunityToolkit, we can unit-test it. We'll put the VM in `Orbital.App` but tests in `Orbital.Core.Tests` which can reference `Orbital.App` for VM testing.

Actually — testing `Orbital.App` from a test project means pulling in Avalonia as a transitive dep, which bloats tests. Simpler: move the VM logic into `Orbital.Core/Interaction/QuickAddViewModel.cs` as a lightweight VM that doesn't depend on Avalonia at all — `ObservableObject` lives in `CommunityToolkit.Mvvm` which is fine to add to Core.

Let me pick: **put the VM in Core.**

- [ ] **Step 1: Add CommunityToolkit.Mvvm to Core**

Run:
```bash
dotnet add src/Orbital.Core/Orbital.Core.csproj package CommunityToolkit.Mvvm --version 8.2.2
```

- [ ] **Step 2: Write failing tests**

```csharp
// tests/Orbital.Core.Tests/ViewModels/QuickAddViewModelTests.cs
namespace Orbital.Core.Tests.ViewModels;

using FluentAssertions;
using Orbital.Core.DateParsing;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;
using Xunit;

public sealed class QuickAddViewModelTests
{
    private static QuickAddViewModel MakeVm(DateOnly today)
    {
        var parser = new DueDateParser(() => today);
        return new QuickAddViewModel(parser);
    }

    [Fact]
    public void Empty_title_means_cannot_submit()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "";
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void Valid_title_empty_due_means_can_submit()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "Buy milk";
        vm.DueInput = "";
        vm.CanSubmit.Should().BeTrue();
        vm.DueParsed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Valid_title_valid_due_means_can_submit_and_parses()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "x";
        vm.DueInput = "tomorrow";
        vm.CanSubmit.Should().BeTrue();
        vm.DueParsed.Date.Should().Be(new DateOnly(2026, 4, 24));
    }

    [Fact]
    public void Valid_title_invalid_due_blocks_submit()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "x";
        vm.DueInput = "asdf";
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void BuildTodo_returns_null_when_not_submittable()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "";
        vm.BuildTodo(order: 0).Should().BeNull();
    }

    [Fact]
    public void BuildTodo_creates_todo_with_parsed_due()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "Buy milk";
        vm.DueInput = "fri";
        var todo = vm.BuildTodo(order: 42);
        todo.Should().NotBeNull();
        todo!.Title.Should().Be("Buy milk");
        todo.DueDate.Should().Be(new DateOnly(2026, 4, 24));
        todo.Order.Should().Be(42);
    }

    [Fact]
    public void Reset_clears_fields()
    {
        var vm = MakeVm(new DateOnly(2026, 4, 23));
        vm.Title = "x";
        vm.DueInput = "tomorrow";
        vm.Reset();
        vm.Title.Should().BeEmpty();
        vm.DueInput.Should().BeEmpty();
    }
}
```

- [ ] **Step 3: Implement the VM**

```csharp
// src/Orbital.Core/ViewModels/QuickAddViewModel.cs
namespace Orbital.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Orbital.Core.DateParsing;
using Orbital.Core.Models;

public sealed partial class QuickAddViewModel : ObservableObject
{
    private readonly DueDateParser parser;

    public QuickAddViewModel(DueDateParser parser)
    {
        this.parser = parser;
    }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DueInput { get; set; } = string.Empty;

    public ParseResult DueParsed => parser.Parse(DueInput);

    public bool CanSubmit =>
        !string.IsNullOrWhiteSpace(Title) && !DueParsed.IsError;

    partial void OnTitleChanged(string value) { OnPropertyChanged(nameof(CanSubmit)); }
    partial void OnDueInputChanged(string value)
    {
        OnPropertyChanged(nameof(DueParsed));
        OnPropertyChanged(nameof(CanSubmit));
    }

    public Todo? BuildTodo(int order)
    {
        if (!CanSubmit) return null;
        return new Todo
        {
            Id = Guid.NewGuid(),
            Title = Title.Trim(),
            DueDate = DueParsed.Date,
            CreatedAt = DateTimeOffset.Now,
            Order = order,
        };
    }

    public void Reset()
    {
        Title = string.Empty;
        DueInput = string.Empty;
    }
}
```

Note: `[ObservableProperty] public partial string Title { get; set; }` is the .NET 9+ partial-property syntax supported by CommunityToolkit.Mvvm 8.2+. If that fails to compile on your exact toolkit version, swap to the field-based form:

```csharp
[ObservableProperty] private string title = string.Empty;
```

...which generates a property named `Title`.

- [ ] **Step 4: Run tests**

Run: `dotnet test`

Expected: All QuickAddViewModel tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Orbital.Core/ViewModels/ tests/Orbital.Core.Tests/ViewModels/ src/Orbital.Core/Orbital.Core.csproj
git commit -m "Add QuickAddViewModel with parse preview and submit-gating tests"
```

---

### Task 17: `QuickAddWindow` XAML + code-behind

**Files:**
- Create: `src/Orbital.App/Views/QuickAddWindow.axaml`
- Create: `src/Orbital.App/Views/QuickAddWindow.axaml.cs`

- [ ] **Step 1: XAML**

```xml
<!-- src/Orbital.App/Views/QuickAddWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Orbital.Core.ViewModels"
        x:Class="Orbital.App.Views.QuickAddWindow"
        x:DataType="vm:QuickAddViewModel"
        Width="420" Height="72"
        SystemDecorations="None"
        CanResize="False"
        Topmost="True"
        ShowInTaskbar="False"
        TransparencyLevelHint="AcrylicBlur"
        Background="#CC1E1E1E"
        CornerRadius="8">
    <Grid Margin="12,8" ColumnDefinitions="*,120,32" RowDefinitions="Auto,*,Auto">
        <TextBox Name="TitleBox"
                 Grid.Column="0" Grid.Row="1"
                 Watermark="What needs doing?"
                 Text="{Binding Title, Mode=TwoWay}"
                 BorderThickness="0" Background="Transparent"
                 FontSize="16" Padding="4" />
        <TextBox Name="DueBox"
                 Grid.Column="1" Grid.Row="1"
                 Watermark="due (optional)"
                 Text="{Binding DueInput, Mode=TwoWay}"
                 BorderThickness="0" Background="Transparent"
                 FontSize="14" Padding="4" />
        <TextBlock Grid.Column="0" Grid.Row="2" Grid.ColumnSpan="2"
                   Text="{Binding DueParsed.Date, StringFormat='→ {0:ddd, MMM d}'}"
                   Foreground="#B0FFFFFF"
                   FontSize="11" Margin="4,0,0,0"
                   IsVisible="{Binding DueParsed.Date, Converter={x:Static ObjectConverters.IsNotNull}}" />
        <TextBlock Grid.Column="0" Grid.Row="2" Grid.ColumnSpan="2"
                   Text="{Binding DueParsed.ErrorMessage, StringFormat='⚠ {0}'}"
                   Foreground="#FFE57373"
                   FontSize="11" Margin="4,0,0,0"
                   IsVisible="{Binding DueParsed.IsError}" />
    </Grid>
</Window>
```

- [ ] **Step 2: Code-behind**

```csharp
// src/Orbital.App/Views/QuickAddWindow.axaml.cs
namespace Orbital.App.Views;

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Orbital.Core.ViewModels;

public partial class QuickAddWindow : Window
{
    public event Action? SubmitRequested;
    public event Action? CancelRequested;

    public QuickAddWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        Deactivated += (_, _) => CancelRequested?.Invoke();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                CancelRequested?.Invoke();
                break;
            case Key.Enter:
                e.Handled = true;
                SubmitRequested?.Invoke();
                break;
        }
    }

    public void FocusTitle()
    {
        var titleBox = this.FindControl<TextBox>("TitleBox");
        titleBox?.Focus();
        titleBox?.SelectAll();
    }
}
```

- [ ] **Step 3: Build to confirm XAML compiles**

Run: `dotnet build`

Expected: 0 errors. If XAML compile errors appear, they're usually missing `xmlns:vm` or a misspelled property — fix and rebuild.

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Views/QuickAddWindow.axaml src/Orbital.App/Views/QuickAddWindow.axaml.cs
git commit -m "Add QuickAddWindow XAML with title, due-date, parse preview"
```

---

### Task 18: `QuickAddController` — wires the window and the model

**Files:**
- Create: `src/Orbital.App/Services/QuickAddController.cs`
- Modify: `src/Orbital.App/App.axaml.cs`

- [ ] **Step 1: Controller**

```csharp
// src/Orbital.App/Services/QuickAddController.cs
namespace Orbital.App.Services;

using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Orbital.App.Views;
using Orbital.Core.DateParsing;
using Orbital.Core.Models;
using Orbital.Core.Ordering;
using Orbital.Core.ViewModels;

public sealed class QuickAddController
{
    private readonly AppHost host;
    private readonly DueDateParser parser = new();
    private QuickAddWindow? window;
    private QuickAddViewModel? vm;

    public QuickAddController(AppHost host) { this.host = host; }

    public void Toggle()
    {
        if (window is { IsVisible: true })
        {
            window.FocusTitle(); // re-invoke while open: focus & select (spec §9)
            window.Activate();
            return;
        }
        ShowNew();
    }

    private void ShowNew()
    {
        window ??= CreateWindow();
        vm!.Reset();
        PositionOnActiveScreen(window);
        window.Show();
        window.Activate();
        window.FocusTitle();
    }

    private QuickAddWindow CreateWindow()
    {
        vm = new QuickAddViewModel(parser);
        var w = new QuickAddWindow { DataContext = vm };
        w.SubmitRequested += OnSubmit;
        w.CancelRequested += () => Dispatcher.UIThread.Post(() => w.Hide());
        return w;
    }

    private void OnSubmit()
    {
        if (vm is null || window is null) return;
        var order = TodoOrdering.NextTopOrder(host.Todos);
        var todo = vm.BuildTodo(order);
        if (todo is null) return;
        host.Todos.Insert(0, todo);
        host.ScheduleSaveTodos();
        window.Hide();
    }

    private static void PositionOnActiveScreen(Window w)
    {
        var screen = w.Screens.ScreenFromWindow(w) ?? w.Screens.Primary;
        if (screen is null) return;
        var r = screen.WorkingArea;
        w.Position = new Avalonia.PixelPoint(
            r.X + (r.Width - (int)w.Width) / 2,
            r.Y + r.Height / 3);
    }
}
```

- [ ] **Step 2: Wire in `App.axaml.cs`**

Add fields: `private QuickAddController? quickAdd;`

In `OnFrameworkInitializationCompleted`, after `AppHost` load:

```csharp
quickAdd = new QuickAddController(Host);

// Hook up the tray-menu and hotkey both to the same Toggle action.
tray.QuickAddRequested += quickAdd.Toggle;

hotkeys!.Register(Host.Settings.QuickAddHotkey, () => quickAdd.Toggle());
```

(Replace the placeholder `Debug.WriteLine` handler added in Task 15.)

- [ ] **Step 3: Manual verification**

Run: `dotnet run --project src/Orbital.App`

Press `Ctrl+Alt+T` (after granting macOS accessibility). Expected: a small dark translucent popup appears centered on top third of screen. Type "Buy milk", Tab, type "tomorrow" → live preview shows "→ Fri, Apr 24" (adjust to your actual today). Press Enter → window disappears.

Check the file:
```bash
cat ~/Library/Application\ Support/Orbital/todos.json | jq
```
Expected: one todo with the title, due tomorrow, `order: 0`, etc.

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Services/QuickAddController.cs src/Orbital.App/App.axaml.cs
git commit -m "Add QuickAddController; Ctrl+Alt+T creates a todo via quick-add popup"
```

---

## Phase 6 — Overlay window

---

### Task 19: `TodoRowViewModel`

**Files:**
- Create: `src/Orbital.Core/ViewModels/TodoRowViewModel.cs`
- Create: `tests/Orbital.Core.Tests/ViewModels/TodoRowViewModelTests.cs`

- [ ] **Step 1: Tests**

```csharp
// tests/Orbital.Core.Tests/ViewModels/TodoRowViewModelTests.cs
namespace Orbital.Core.Tests.ViewModels;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;
using Xunit;

public sealed class TodoRowViewModelTests
{
    private static Todo MakeTodo(DateOnly? due = null, bool completed = false) => new()
    {
        Id = Guid.NewGuid(),
        Title = "x",
        DueDate = due,
        CreatedAt = DateTimeOffset.UtcNow,
        Order = 0,
        CompletedAt = completed ? DateTimeOffset.UtcNow : null,
    };

    [Fact]
    public void Overdue_when_due_before_today()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(new DateOnly(2026, 4, 20)), () => today);
        vm.IsOverdue.Should().BeTrue();
        vm.IsDueToday.Should().BeFalse();
    }

    [Fact]
    public void Due_today_when_due_equals_today()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(today), () => today);
        vm.IsDueToday.Should().BeTrue();
        vm.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void Completed_not_overdue()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(new DateOnly(2026, 4, 20), completed: true), () => today);
        vm.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void DueLabel_shows_em_dash_when_no_due()
    {
        var today = new DateOnly(2026, 4, 23);
        var vm = new TodoRowViewModel(MakeTodo(null), () => today);
        vm.DueLabel.Should().Be("—");
    }

    [Fact]
    public void DueLabel_shows_Today_or_Tomorrow_or_weekday_or_full()
    {
        var today = new DateOnly(2026, 4, 23); // Thu
        new TodoRowViewModel(MakeTodo(today), () => today).DueLabel.Should().Be("Today");
        new TodoRowViewModel(MakeTodo(today.AddDays(1)), () => today).DueLabel.Should().Be("Tomorrow");
        new TodoRowViewModel(MakeTodo(today.AddDays(2)), () => today).DueLabel.Should().Be("Sat");
        new TodoRowViewModel(MakeTodo(today.AddDays(8)), () => today).DueLabel.Should().Be("May 1"); // not a weekday rendering — >7 days
    }
}
```

- [ ] **Step 2: Implement**

```csharp
// src/Orbital.Core/ViewModels/TodoRowViewModel.cs
namespace Orbital.Core.ViewModels;

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Orbital.Core.Models;

public sealed partial class TodoRowViewModel : ObservableObject
{
    private readonly Func<DateOnly> today;

    public TodoRowViewModel(Todo model, Func<DateOnly>? todayProvider = null)
    {
        Model = model;
        today = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Today));
    }

    public Todo Model { get; }

    public string Title
    {
        get => Model.Title;
        set
        {
            if (Model.Title == value) return;
            Model.Title = value;
            OnPropertyChanged();
        }
    }

    public DateOnly? DueDate
    {
        get => Model.DueDate;
        set
        {
            if (Model.DueDate == value) return;
            Model.DueDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DueLabel));
            OnPropertyChanged(nameof(IsOverdue));
            OnPropertyChanged(nameof(IsDueToday));
        }
    }

    public bool IsCompleted
    {
        get => Model.IsCompleted;
        set
        {
            if (value == Model.IsCompleted) return;
            Model.CompletedAt = value ? DateTimeOffset.Now : null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverdue));
        }
    }

    public string DueLabel
    {
        get
        {
            if (DueDate is null) return "—";
            var d = DueDate.Value;
            var t = today();
            if (d == t) return "Today";
            if (d == t.AddDays(1)) return "Tomorrow";
            var diff = d.DayNumber - t.DayNumber;
            if (diff > 0 && diff < 7)
                return d.ToString("ddd", CultureInfo.InvariantCulture);
            return d.ToString("MMM d", CultureInfo.InvariantCulture);
        }
    }

    public bool IsOverdue => DueDate is { } d && !IsCompleted && d < today();
    public bool IsDueToday => DueDate is { } d && d == today();
}
```

- [ ] **Step 3: Run tests, commit**

```bash
dotnet test
git add src/Orbital.Core/ViewModels/TodoRowViewModel.cs tests/Orbital.Core.Tests/ViewModels/TodoRowViewModelTests.cs
git commit -m "Add TodoRowViewModel with due label, overdue, and due-today tests"
```

---

### Task 20: `OverlayViewModel`

**Files:**
- Create: `src/Orbital.Core/ViewModels/OverlayViewModel.cs`
- Create: `tests/Orbital.Core.Tests/ViewModels/OverlayViewModelTests.cs`

The overlay VM owns the filtered rows presented to the view (active-only or active+completed), and exposes commands. It does NOT own storage; it calls back to the host via callbacks passed in.

- [ ] **Step 1: Tests**

```csharp
// tests/Orbital.Core.Tests/ViewModels/OverlayViewModelTests.cs
namespace Orbital.Core.Tests.ViewModels;

using FluentAssertions;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;
using System.Collections.ObjectModel;
using Xunit;

public sealed class OverlayViewModelTests
{
    private static Todo T(int order, string title, bool completed = false) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        CreatedAt = DateTimeOffset.UtcNow,
        Order = order,
        CompletedAt = completed ? DateTimeOffset.UtcNow : null,
    };

    private static OverlayViewModel Make(ObservableCollection<Todo> todos, bool showCompleted = false)
    {
        var vm = new OverlayViewModel(todos, () => new DateOnly(2026, 4, 23))
        {
            ShowCompleted = showCompleted,
        };
        return vm;
    }

    [Fact]
    public void Rows_shows_only_active_sorted_by_order_ascending()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(2, "c"), T(0, "a"), T(1, "b"), T(-1, "first"),
        };
        var vm = Make(todos);
        vm.Rows.Select(r => r.Title).Should().Equal("first", "a", "b", "c");
    }

    [Fact]
    public void Completed_items_hidden_by_default_visible_when_ShowCompleted()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "done", completed: true), T(1, "active"),
        };
        var vm = Make(todos);
        vm.Rows.Select(r => r.Title).Should().Equal("active");

        vm.ShowCompleted = true;
        vm.Rows.Select(r => r.Title).Should().Equal("active", "done");
    }

    [Fact]
    public void ToggleComplete_moves_row_out_of_active_when_ShowCompleted_is_false()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"), T(1, "b"),
        };
        var vm = Make(todos);
        vm.ToggleComplete(vm.Rows[0]);
        vm.Rows.Select(r => r.Title).Should().Equal("b");
    }

    [Fact]
    public void Delete_removes_from_todos_and_rows()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"), T(1, "b"),
        };
        var vm = Make(todos);
        vm.Delete(vm.Rows[0]);
        todos.Select(t => t.Title).Should().Equal("b");
        vm.Rows.Select(r => r.Title).Should().Equal("b");
    }

    [Fact]
    public void Undo_after_delete_restores_row()
    {
        var todos = new ObservableCollection<Todo>
        {
            T(0, "a"),
        };
        var vm = Make(todos);
        vm.Delete(vm.Rows[0]);
        vm.Undo();
        todos.Should().ContainSingle(t => t.Title == "a");
        vm.Rows.Should().ContainSingle();
    }
}
```

- [ ] **Step 2: Implement**

```csharp
// src/Orbital.Core/ViewModels/OverlayViewModel.cs
namespace Orbital.Core.ViewModels;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Orbital.Core.Models;

public sealed partial class OverlayViewModel : ObservableObject
{
    private readonly ObservableCollection<Todo> source;
    private readonly Func<DateOnly> today;
    private Action? lastUndo;

    public ObservableCollection<TodoRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    public partial bool ShowCompleted { get; set; }

    public OverlayViewModel(ObservableCollection<Todo> source, Func<DateOnly>? todayProvider = null)
    {
        this.source = source;
        this.today = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Today));
        source.CollectionChanged += OnSourceChanged;
        Rebuild();
    }

    partial void OnShowCompletedChanged(bool value) => Rebuild();

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    public event Action? TodosMutated;

    private void Rebuild()
    {
        IEnumerable<Todo> active = source.Where(t => !t.IsCompleted).OrderBy(t => t.Order);
        IEnumerable<Todo> completed = ShowCompleted
            ? source.Where(t => t.IsCompleted).OrderByDescending(t => t.CompletedAt)
            : Enumerable.Empty<Todo>();

        Rows.Clear();
        foreach (var t in active.Concat(completed))
            Rows.Add(new TodoRowViewModel(t, today));
    }

    public void ToggleComplete(TodoRowViewModel row)
    {
        row.IsCompleted = !row.IsCompleted;
        TodosMutated?.Invoke();
        Rebuild();
    }

    public void Delete(TodoRowViewModel row)
    {
        var todo = row.Model;
        var index = source.IndexOf(todo);
        if (index < 0) return;
        source.RemoveAt(index); // triggers OnSourceChanged -> Rebuild
        lastUndo = () =>
        {
            source.Insert(Math.Min(index, source.Count), todo);
            TodosMutated?.Invoke();
        };
        TodosMutated?.Invoke();
    }

    public void Snooze(TodoRowViewModel row)
    {
        var t = today();
        row.DueDate = row.DueDate is { } d ? d.AddDays(1) : t.AddDays(1);
        TodosMutated?.Invoke();
    }

    public void Undo()
    {
        var action = lastUndo;
        lastUndo = null;
        action?.Invoke();
    }
}
```

- [ ] **Step 3: Run tests, commit**

```bash
dotnet test
git add src/Orbital.Core/ViewModels/OverlayViewModel.cs tests/Orbital.Core.Tests/ViewModels/OverlayViewModelTests.cs
git commit -m "Add OverlayViewModel with Rows, ShowCompleted, toggle, delete, undo"
```

---

### Task 21: `OverlayWindow` XAML + code-behind

**Files:**
- Create: `src/Orbital.App/Views/OverlayWindow.axaml`
- Create: `src/Orbital.App/Views/OverlayWindow.axaml.cs`

- [ ] **Step 1: XAML**

```xml
<!-- src/Orbital.App/Views/OverlayWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Orbital.Core.ViewModels"
        x:Class="Orbital.App.Views.OverlayWindow"
        x:DataType="vm:OverlayViewModel"
        Width="360" Height="520"
        SystemDecorations="None"
        CanResize="False"
        Topmost="True"
        ShowInTaskbar="False"
        TransparencyLevelHint="AcrylicBlur"
        Background="#CC1E1E1E"
        CornerRadius="10">
    <Border CornerRadius="10" BorderBrush="#33FFFFFF" BorderThickness="1">
        <DockPanel LastChildFill="True">
            <!-- Header -->
            <Grid DockPanel.Dock="Top" Height="36" ColumnDefinitions="*,Auto,Auto" Margin="12,6">
                <TextBlock Grid.Column="0" Text="Orbital" Foreground="White"
                           FontWeight="SemiBold" VerticalAlignment="Center" />
                <Button Name="SettingsButton" Grid.Column="1" Width="24" Height="24"
                        Background="Transparent" BorderThickness="0" Content="⚙︎"
                        Foreground="#B0FFFFFF" />
                <Button Name="CloseButton" Grid.Column="2" Width="24" Height="24"
                        Background="Transparent" BorderThickness="0" Content="✕"
                        Foreground="#B0FFFFFF" />
            </Grid>
            <!-- Footer -->
            <Grid DockPanel.Dock="Bottom" Height="32" ColumnDefinitions="*,Auto" Margin="12,4">
                <TextBlock Grid.Column="0" Foreground="#80FFFFFF" FontSize="11"
                           VerticalAlignment="Center"
                           Text="{Binding Rows.Count, StringFormat='{}{0} items'}" />
                <ToggleButton Grid.Column="1" Content="show ✓" Padding="6,2"
                              Foreground="#B0FFFFFF" Background="Transparent" BorderThickness="0"
                              FontSize="11"
                              IsChecked="{Binding ShowCompleted, Mode=TwoWay}" />
            </Grid>
            <!-- List -->
            <ListBox Name="RowList"
                     ItemsSource="{Binding Rows}"
                     Background="Transparent" BorderThickness="0"
                     SelectionMode="Single"
                     Padding="4"
                     ScrollViewer.HorizontalScrollBarVisibility="Disabled">
                <ListBox.ItemTemplate>
                    <DataTemplate x:DataType="vm:TodoRowViewModel">
                        <Grid ColumnDefinitions="Auto,*,Auto" Margin="4,2" Height="32">
                            <CheckBox Grid.Column="0"
                                      IsChecked="{Binding IsCompleted, Mode=TwoWay}"
                                      VerticalAlignment="Center" Margin="0,0,8,0"/>
                            <TextBlock Grid.Column="1"
                                       Text="{Binding Title}"
                                       Foreground="White"
                                       VerticalAlignment="Center"
                                       TextTrimming="CharacterEllipsis">
                                <TextBlock.Styles>
                                    <Style Selector="TextBlock[Tag='true']">
                                        <Setter Property="TextDecorations" Value="Strikethrough" />
                                        <Setter Property="Foreground" Value="#80FFFFFF" />
                                    </Style>
                                </TextBlock.Styles>
                            </TextBlock>
                            <Border Grid.Column="2" Padding="6,2" CornerRadius="4"
                                    Background="#22FFFFFF" VerticalAlignment="Center">
                                <TextBlock Text="{Binding DueLabel}"
                                           FontSize="11" Foreground="#D0FFFFFF" />
                            </Border>
                        </Grid>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </DockPanel>
    </Border>
</Window>
```

- [ ] **Step 2: Code-behind (minimal)**

```csharp
// src/Orbital.App/Views/OverlayWindow.axaml.cs
namespace Orbital.App.Views;

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Orbital.Core.ViewModels;

public partial class OverlayWindow : Window
{
    public event Action? CloseRequested;
    public event Action? SettingsRequested;

    public OverlayWindow()
    {
        InitializeComponent();
        this.KeyDown += OnKeyDown;
        this.Deactivated += (_, _) => CloseRequested?.Invoke();
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => CloseRequested?.Invoke();
        this.FindControl<Button>("SettingsButton")!.Click += (_, _) => SettingsRequested?.Invoke();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var list = this.FindControl<ListBox>("RowList");
        var vm = (OverlayViewModel)this.DataContext!;
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                CloseRequested?.Invoke();
                break;
            case Key.Space:
                if (list?.SelectedItem is TodoRowViewModel r1) { vm.ToggleComplete(r1); e.Handled = true; }
                break;
            case Key.Delete:
            case Key.Back:
                if (list?.SelectedItem is TodoRowViewModel r2) { vm.Delete(r2); e.Handled = true; }
                break;
            case Key.S:
                if (list?.SelectedItem is TodoRowViewModel r3) { vm.Snooze(r3); e.Handled = true; }
                break;
            case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta):
                vm.Undo(); e.Handled = true;
                break;
        }
    }
}
```

- [ ] **Step 3: Build, no runtime check yet — we wire it next**

Run: `dotnet build`

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Views/OverlayWindow.axaml src/Orbital.App/Views/OverlayWindow.axaml.cs
git commit -m "Add OverlayWindow XAML + keyboard shortcuts code-behind"
```

---

### Task 22: `OverlayController` — positions window, toggles visibility, saves on mutation

**Files:**
- Create: `src/Orbital.App/Services/OverlayController.cs`
- Modify: `src/Orbital.App/App.axaml.cs`

- [ ] **Step 1: Controller**

```csharp
// src/Orbital.App/Services/OverlayController.cs
namespace Orbital.App.Services;

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Orbital.App.Views;
using Orbital.Core.Models;
using Orbital.Core.ViewModels;

public sealed class OverlayController
{
    private readonly AppHost host;
    private OverlayWindow? window;
    private OverlayViewModel? vm;

    public event Action? SettingsRequested;

    public OverlayController(AppHost host) { this.host = host; }

    public void Toggle()
    {
        if (window is { IsVisible: true })
        {
            window.Hide();
            return;
        }
        Show();
    }

    private void Show()
    {
        EnsureWindow();
        PositionOnActiveScreen(window!);
        window!.Show();
        window.Activate();
    }

    private void EnsureWindow()
    {
        if (window is not null) return;
        vm = new OverlayViewModel(host.Todos) { ShowCompleted = host.Settings.ShowCompleted };
        vm.TodosMutated += () => host.ScheduleSaveTodos();
        window = new OverlayWindow { DataContext = vm };
        window.CloseRequested += () => Dispatcher.UIThread.Post(() => window.Hide());
        window.SettingsRequested += () => SettingsRequested?.Invoke();
    }

    private void PositionOnActiveScreen(OverlayWindow w)
    {
        var screen = w.Screens.ScreenFromWindow(w) ?? w.Screens.Primary;
        if (screen is null) return;
        var wa = screen.WorkingArea;
        const int margin = 24;
        var (x, y) = host.Settings.OverlayPosition switch
        {
            OverlayPosition.TopRight    => (wa.Right - (int)w.Width - margin, wa.Y + margin),
            OverlayPosition.TopLeft     => (wa.X + margin,                     wa.Y + margin),
            OverlayPosition.BottomRight => (wa.Right - (int)w.Width - margin, wa.Bottom - (int)w.Height - margin),
            OverlayPosition.BottomLeft  => (wa.X + margin,                     wa.Bottom - (int)w.Height - margin),
            _ => (wa.Right - (int)w.Width - margin, wa.Y + margin),
        };
        w.Position = new PixelPoint(x, y);
    }
}
```

- [ ] **Step 2: Wire in `App.axaml.cs`**

Add field: `private OverlayController? overlay;`

In `OnFrameworkInitializationCompleted` (replace the placeholder for overlay hotkey from Task 15):

```csharp
overlay = new OverlayController(Host);
tray!.ToggleOverlayRequested += overlay.Toggle;
hotkeys!.Register(Host.Settings.ToggleOverlayHotkey, () => overlay.Toggle());
```

- [ ] **Step 3: Manual verification**

Run: `dotnet run --project src/Orbital.App`

1. Add a todo via Quick-add (Task 18 flow).
2. Press `Ctrl+Alt+L`. Expected: the overlay appears top-right, shows the todo with its due-date chip.
3. Press `Ctrl+Alt+L` again. Expected: it hides.
4. Re-open and click the checkbox. Expected: the row disappears (completed-hidden default).
5. Click the footer `show ✓` toggle. Expected: completed rows reappear, struck through.
6. Select a row (click), press Del → the row vanishes. Press Ctrl+Z → it returns.
7. Select a row, press S → a due date appears/advances.

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Services/OverlayController.cs src/Orbital.App/App.axaml.cs
git commit -m "Add OverlayController; Ctrl+Alt+L toggles positioned overlay"
```

---

### Task 23: Drag-and-drop reorder in the overlay list

**Files:**
- Modify: `src/Orbital.App/Views/OverlayWindow.axaml`
- Modify: `src/Orbital.App/Views/OverlayWindow.axaml.cs`
- Modify: `src/Orbital.Core/ViewModels/OverlayViewModel.cs`

Avalonia's `ListBox` doesn't ship drag-to-reorder by default. We implement it manually with `PointerPressed` / `PointerMoved` / `PointerReleased` and compute the drop index from the pointer position.

- [ ] **Step 1: Add a `Reorder` method to `OverlayViewModel`**

In `OverlayViewModel.cs`, add:

```csharp
public void Reorder(int fromIndex, int toIndex)
{
    if (fromIndex < 0 || toIndex < 0) return;
    var active = source.Where(t => !t.IsCompleted).OrderBy(t => t.Order).ToList();
    if (fromIndex >= active.Count || toIndex >= active.Count) return;
    Orbital.Core.Ordering.TodoOrdering.ReorderActive(active, fromIndex, toIndex);
    // active now holds correct Order values; the models are references, so source's items are updated.
    TodosMutated?.Invoke();
    Rebuild();
}
```

Add a test in `OverlayViewModelTests.cs`:

```csharp
[Fact]
public void Reorder_changes_row_order()
{
    var todos = new ObservableCollection<Todo>
    {
        T(0, "a"), T(1, "b"), T(2, "c"),
    };
    var vm = Make(todos);
    vm.Reorder(0, 2);
    vm.Rows.Select(r => r.Title).Should().Equal("b", "c", "a");
}
```

Run: `dotnet test` — expect green.

- [ ] **Step 2: Wire drag handlers in the OverlayWindow code-behind**

Add to `OverlayWindow.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;

// ... in constructor, after other event hooks:
var list = this.FindControl<ListBox>("RowList")!;
list.AddHandler(PointerPressedEvent, OnListPointerPressed, RoutingStrategies.Tunnel);
list.AddHandler(PointerMovedEvent, OnListPointerMoved, RoutingStrategies.Tunnel);
list.AddHandler(PointerReleasedEvent, OnListPointerReleased, RoutingStrategies.Tunnel);

private bool isDragging;
private int dragFromIndex = -1;
private Point dragStart;

private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
{
    var list = (ListBox)sender!;
    var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>();
    if (item is null) return;
    dragFromIndex = list.IndexFromContainer(item);
    dragStart = e.GetPosition(list);
}

private void OnListPointerMoved(object? sender, PointerEventArgs e)
{
    if (dragFromIndex < 0) return;
    var list = (ListBox)sender!;
    var pos = e.GetPosition(list);
    if (!isDragging && (pos - dragStart).Length > 6)
        isDragging = true;
}

private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
{
    try
    {
        if (!isDragging || dragFromIndex < 0) return;
        var list = (ListBox)sender!;
        var pos = e.GetPosition(list);
        // Compute drop index as clamp(row under pointer, 0, count-1).
        var hit = list.GetVisualsAt(pos).OfType<Visual>()
                      .Select(v => v.FindAncestorOfType<ListBoxItem>())
                      .FirstOrDefault(x => x is not null);
        int toIndex = hit is not null ? list.IndexFromContainer(hit) : list.ItemCount - 1;
        if (toIndex < 0) toIndex = 0;

        if (DataContext is OverlayViewModel vm && toIndex != dragFromIndex)
            vm.Reorder(dragFromIndex, toIndex);
    }
    finally
    {
        isDragging = false;
        dragFromIndex = -1;
    }
}
```

Add using directives at the top: `using Avalonia.Interactivity;`, `using Avalonia.VisualTree;`, `using System.Linq;`.

- [ ] **Step 3: Manual verification**

Run the app, add three todos, open the overlay, drag the first to the bottom. Expected: order changes, persists across restart (check `todos.json` has the new `order` values).

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Views/OverlayWindow.axaml.cs \
        src/Orbital.Core/ViewModels/OverlayViewModel.cs \
        tests/Orbital.Core.Tests/ViewModels/OverlayViewModelTests.cs
git commit -m "Add drag-to-reorder in overlay via pointer events + ReorderActive"
```

---

### Task 24: Inline edit title and due date

**Files:**
- Modify: `src/Orbital.App/Views/OverlayWindow.axaml` (template tweak)
- Create: `src/Orbital.App/Views/EditableRow.axaml` (a `UserControl` wrapping a row in view/edit toggle)

Avalonia doesn't have a built-in inline-edit cell. The simplest path: in the `DataTemplate`, show a `TextBlock` normally and switch to a `TextBox` while a per-row `IsEditing` is true. Same approach for the due-date chip.

- [ ] **Step 1: Extend `TodoRowViewModel`** with `IsEditing` + `IsEditingDue` + a parser for due-date editing.

In `TodoRowViewModel.cs`:

```csharp
[ObservableProperty] public partial bool IsEditingTitle { get; set; }
[ObservableProperty] public partial bool IsEditingDue { get; set; }
[ObservableProperty] public partial string DueEditBuffer { get; set; } = string.Empty;

private readonly DueDateParser parser;

public TodoRowViewModel(Todo model, DueDateParser? parser = null, Func<DateOnly>? todayProvider = null)
{
    Model = model;
    this.today = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Today));
    this.parser = parser ?? new DueDateParser(this.today);
}

public void BeginEditTitle() => IsEditingTitle = true;
public void CommitTitle(string newTitle)
{
    Title = newTitle.Trim();
    IsEditingTitle = false;
}
public void CancelEditTitle() { IsEditingTitle = false; OnPropertyChanged(nameof(Title)); }

public void BeginEditDue()
{
    DueEditBuffer = DueDate?.ToString("yyyy-MM-dd") ?? string.Empty;
    IsEditingDue = true;
}
public bool TryCommitDue()
{
    var r = parser.Parse(DueEditBuffer);
    if (r.IsError) return false;
    DueDate = r.Date;
    IsEditingDue = false;
    return true;
}
public void CancelEditDue() => IsEditingDue = false;
```

(You'll need to update the constructor used in `OverlayViewModel.Rebuild` to pass the parser. Since the overlay VM doesn't carry one, instantiate a default `DueDateParser()` in `Rebuild` and pass it.)

- [ ] **Step 2: Add a test**

```csharp
[Fact]
public void TryCommitDue_parses_and_sets_date()
{
    var today = new DateOnly(2026, 4, 23);
    var todo = new Todo { Id = Guid.NewGuid(), Title = "x",
                          CreatedAt = DateTimeOffset.UtcNow, Order = 0 };
    var vm = new TodoRowViewModel(todo, new DueDateParser(() => today), () => today);
    vm.BeginEditDue();
    vm.DueEditBuffer = "tomorrow";
    vm.TryCommitDue().Should().BeTrue();
    vm.DueDate.Should().Be(new DateOnly(2026, 4, 24));
}

[Fact]
public void TryCommitDue_returns_false_on_invalid()
{
    var today = new DateOnly(2026, 4, 23);
    var todo = new Todo { Id = Guid.NewGuid(), Title = "x",
                          CreatedAt = DateTimeOffset.UtcNow, Order = 0 };
    var vm = new TodoRowViewModel(todo, new DueDateParser(() => today), () => today);
    vm.BeginEditDue();
    vm.DueEditBuffer = "asdf";
    vm.TryCommitDue().Should().BeFalse();
    vm.IsEditingDue.Should().BeTrue();
}
```

- [ ] **Step 3: Replace the row template in `OverlayWindow.axaml`** with a template that swaps a `TextBlock` for a `TextBox` when `IsEditingTitle`, and similarly for the due chip:

```xml
<DataTemplate x:DataType="vm:TodoRowViewModel">
    <Grid ColumnDefinitions="Auto,*,Auto" Margin="4,2" Height="32">
        <CheckBox Grid.Column="0"
                  IsChecked="{Binding IsCompleted, Mode=TwoWay}"
                  VerticalAlignment="Center" Margin="0,0,8,0"/>

        <Grid Grid.Column="1">
            <TextBlock Text="{Binding Title}"
                       Foreground="White"
                       VerticalAlignment="Center"
                       TextTrimming="CharacterEllipsis"
                       IsVisible="{Binding !IsEditingTitle}" />
            <TextBox Text="{Binding Title, Mode=TwoWay}"
                     BorderThickness="0" Background="#22000000"
                     IsVisible="{Binding IsEditingTitle}"
                     Padding="2" />
        </Grid>

        <Grid Grid.Column="2">
            <Border Padding="6,2" CornerRadius="4"
                    Background="#22FFFFFF"
                    VerticalAlignment="Center"
                    IsVisible="{Binding !IsEditingDue}">
                <TextBlock Text="{Binding DueLabel}"
                           FontSize="11" Foreground="#D0FFFFFF" />
            </Border>
            <TextBox Text="{Binding DueEditBuffer, Mode=TwoWay}"
                     Width="90" BorderThickness="0" Background="#22000000"
                     IsVisible="{Binding IsEditingDue}" Padding="2" />
        </Grid>
    </Grid>
</DataTemplate>
```

- [ ] **Step 4: Wire keyboard entry into edit mode**

In `OverlayWindow.axaml.cs` `OnKeyDown`, add:

```csharp
case Key.Enter:
    if (list?.SelectedItem is TodoRowViewModel re)
    {
        if (re.IsEditingTitle) { re.CommitTitle(re.Title); e.Handled = true; }
        else if (re.IsEditingDue) { re.TryCommitDue(); e.Handled = true; }
        else { re.BeginEditTitle(); e.Handled = true; }
    }
    break;
```

And add a `PointerPressed` handler on the due-chip to enter due-edit on click. Simplest path: add a handler to the grid at column 2 by name — or a right-click context menu. For MVP: double-click the row enters title edit (already covered by Enter) and the user uses a small keyboard shortcut `Ctrl+D` to start due edit:

```csharp
case Key.D when e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta):
    if (list?.SelectedItem is TodoRowViewModel rd) { rd.BeginEditDue(); e.Handled = true; }
    break;
```

- [ ] **Step 5: Manual verification**

Run the app, open overlay, select a row:
- Enter → title becomes editable; edit; Enter → title persists.
- `Ctrl+D` (or `Cmd+D` on mac) → due becomes editable; type "fri"; Enter → due updates; "asdf" + Enter → stays in edit mode, no update.

- [ ] **Step 6: Commit**

```bash
git add src/Orbital.Core/ViewModels/TodoRowViewModel.cs \
        src/Orbital.App/Views/OverlayWindow.axaml \
        src/Orbital.App/Views/OverlayWindow.axaml.cs \
        tests/Orbital.Core.Tests/ViewModels/TodoRowViewModelTests.cs
git commit -m "Add inline edit for row title and due date with keyboard entry"
```

---

## Phase 7 — Settings window

---

### Task 25: `SettingsWindow` with hotkey rebind, overlay position, start-at-login toggle

**Files:**
- Create: `src/Orbital.App/Views/SettingsWindow.axaml`
- Create: `src/Orbital.App/Views/SettingsWindow.axaml.cs`
- Create: `src/Orbital.Core/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: Settings VM**

```csharp
// src/Orbital.Core/ViewModels/SettingsViewModel.cs
namespace Orbital.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Orbital.Core.Models;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings settings;

    public SettingsViewModel(AppSettings settings)
    {
        this.settings = settings;
        quickAddHotkey = Describe(settings.QuickAddHotkey);
        toggleOverlayHotkey = Describe(settings.ToggleOverlayHotkey);
        overlayPosition = settings.OverlayPosition;
        overlayAutoHideOnFocusLoss = settings.OverlayAutoHideOnFocusLoss;
        showCompleted = settings.ShowCompleted;
        startAtLogin = settings.StartAtLogin;
    }

    [ObservableProperty] public partial string QuickAddHotkey { get; set; }
    [ObservableProperty] public partial string ToggleOverlayHotkey { get; set; }
    [ObservableProperty] public partial OverlayPosition OverlayPosition { get; set; }
    [ObservableProperty] public partial bool OverlayAutoHideOnFocusLoss { get; set; }
    [ObservableProperty] public partial bool ShowCompleted { get; set; }
    [ObservableProperty] public partial bool StartAtLogin { get; set; }

    public AppSettings Build()
    {
        return settings with
        {
            QuickAddHotkey = ParseOrKeep(QuickAddHotkey, settings.QuickAddHotkey),
            ToggleOverlayHotkey = ParseOrKeep(ToggleOverlayHotkey, settings.ToggleOverlayHotkey),
            OverlayPosition = OverlayPosition,
            OverlayAutoHideOnFocusLoss = OverlayAutoHideOnFocusLoss,
            ShowCompleted = ShowCompleted,
            StartAtLogin = StartAtLogin,
        };
    }

    private static string Describe(HotkeyBinding b)
    {
        var parts = new List<string>();
        if (b.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (b.Modifiers.HasFlag(HotkeyModifiers.Alt))     parts.Add("Alt");
        if (b.Modifiers.HasFlag(HotkeyModifiers.Shift))   parts.Add("Shift");
        if (b.Modifiers.HasFlag(HotkeyModifiers.Meta))    parts.Add("Meta");
        parts.Add(b.KeyName);
        return string.Join("+", parts);
    }

    private static HotkeyBinding ParseOrKeep(string text, HotkeyBinding fallback)
    {
        var tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return fallback;
        var mods = HotkeyModifiers.None;
        string key = "";
        foreach (var t in tokens)
        {
            switch (t.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= HotkeyModifiers.Control; break;
                case "alt" or "option":   mods |= HotkeyModifiers.Alt; break;
                case "shift":             mods |= HotkeyModifiers.Shift; break;
                case "meta" or "cmd" or "win": mods |= HotkeyModifiers.Meta; break;
                default: key = t.ToUpperInvariant(); break;
            }
        }
        if (mods == HotkeyModifiers.None || string.IsNullOrEmpty(key)) return fallback;
        return new HotkeyBinding(mods, key);
    }
}
```

- [ ] **Step 2: XAML**

```xml
<!-- src/Orbital.App/Views/SettingsWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Orbital.Core.ViewModels"
        xmlns:m="using:Orbital.Core.Models"
        x:Class="Orbital.App.Views.SettingsWindow"
        x:DataType="vm:SettingsViewModel"
        Title="Orbital — Settings"
        Width="480" Height="380"
        CanResize="False">
    <StackPanel Margin="24" Spacing="12">
        <TextBlock Text="Hotkeys" FontSize="14" FontWeight="SemiBold" />
        <Grid ColumnDefinitions="140,*" RowDefinitions="Auto,Auto" RowSpacing="8">
            <TextBlock Text="Quick add"  Grid.Row="0" Grid.Column="0" VerticalAlignment="Center"/>
            <TextBox Text="{Binding QuickAddHotkey, Mode=TwoWay}" Grid.Row="0" Grid.Column="1"/>
            <TextBlock Text="Show overlay" Grid.Row="1" Grid.Column="0" VerticalAlignment="Center"/>
            <TextBox Text="{Binding ToggleOverlayHotkey, Mode=TwoWay}" Grid.Row="1" Grid.Column="1"/>
        </Grid>

        <TextBlock Text="Overlay" FontSize="14" FontWeight="SemiBold" Margin="0,12,0,0" />
        <Grid ColumnDefinitions="140,*" RowDefinitions="Auto,Auto,Auto" RowSpacing="8">
            <TextBlock Text="Position" Grid.Row="0" Grid.Column="0" VerticalAlignment="Center"/>
            <ComboBox Grid.Row="0" Grid.Column="1"
                      SelectedItem="{Binding OverlayPosition, Mode=TwoWay}">
                <m:OverlayPosition>TopRight</m:OverlayPosition>
                <m:OverlayPosition>TopLeft</m:OverlayPosition>
                <m:OverlayPosition>BottomRight</m:OverlayPosition>
                <m:OverlayPosition>BottomLeft</m:OverlayPosition>
            </ComboBox>
            <CheckBox Grid.Row="1" Grid.Column="1" Content="Hide when focus moves away"
                      IsChecked="{Binding OverlayAutoHideOnFocusLoss, Mode=TwoWay}"/>
            <CheckBox Grid.Row="2" Grid.Column="1" Content="Show completed todos by default"
                      IsChecked="{Binding ShowCompleted, Mode=TwoWay}"/>
        </Grid>

        <TextBlock Text="Startup" FontSize="14" FontWeight="SemiBold" Margin="0,12,0,0" />
        <CheckBox Content="Start Orbital at login" IsChecked="{Binding StartAtLogin, Mode=TwoWay}" />

        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8" Margin="0,24,0,0">
            <Button Name="CancelButton" Content="Cancel" />
            <Button Name="SaveButton" Content="Save" IsDefault="True" />
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 3: Code-behind**

```csharp
// src/Orbital.App/Views/SettingsWindow.axaml.cs
namespace Orbital.App.Views;

using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public partial class SettingsWindow : Window
{
    public event Action? SaveRequested;
    public event Action? CancelRequested;

    public SettingsWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("SaveButton")!.Click += (_, _) => SaveRequested?.Invoke();
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => CancelRequested?.Invoke();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 4: Controller in `App.axaml.cs`**

Add a helper method:

```csharp
private void ShowSettings()
{
    if (Host is null) return;
    var vm = new SettingsViewModel(Host.Settings);
    var w = new SettingsWindow { DataContext = vm };
    w.SaveRequested += async () =>
    {
        Host.Settings = vm.Build();
        await Host.SaveSettingsAsync();
        w.Close();
    };
    w.CancelRequested += () => w.Close();
    w.Show();
}
```

Wire from tray + overlay:
```csharp
tray!.SettingsRequested += ShowSettings;
overlay!.SettingsRequested += ShowSettings;
```

- [ ] **Step 5: Manual verification**

Run, right-click tray → Settings. Edit "Quick add" to `Ctrl+Alt+N`, Save, quit, relaunch, press `Ctrl+Alt+N` → quick-add opens. (Hotkey re-registration after Save is a known limitation for this task; a full re-register on save is Task 26.)

- [ ] **Step 6: Commit**

```bash
git add src/Orbital.Core/ViewModels/SettingsViewModel.cs \
        src/Orbital.App/Views/SettingsWindow.axaml \
        src/Orbital.App/Views/SettingsWindow.axaml.cs \
        src/Orbital.App/App.axaml.cs
git commit -m "Add Settings window with hotkey rebind, overlay position, startup toggle"
```

---

### Task 26: Hotkey re-registration on settings save

**Files:**
- Modify: `src/Orbital.App/App.axaml.cs`

- [ ] **Step 1: Track hotkey handles**

In `App.axaml.cs`, add fields:

```csharp
private IDisposable? quickAddHotkeyReg;
private IDisposable? overlayHotkeyReg;
```

Replace the `hotkeys!.Register(...)` calls set up in Tasks 18/22 with:

```csharp
RegisterHotkeys();

void RegisterHotkeys()
{
    quickAddHotkeyReg?.Dispose();
    overlayHotkeyReg?.Dispose();
    quickAddHotkeyReg = hotkeys!.Register(Host!.Settings.QuickAddHotkey, () => quickAdd!.Toggle());
    overlayHotkeyReg  = hotkeys.Register(Host.Settings.ToggleOverlayHotkey, () => overlay!.Toggle());
}
```

Hook settings changes:
```csharp
Host.SettingsChanged += RegisterHotkeys;
```

- [ ] **Step 2: Manual verification**

Rebind in Settings → click Save → immediately press the new binding without restarting. The new hotkey should fire.

- [ ] **Step 3: Commit**

```bash
git add src/Orbital.App/App.axaml.cs
git commit -m "Re-register global hotkeys whenever settings change"
```

---

## Phase 8 — Autostart

---

### Task 27: `IAutoStartService` with per-OS implementations

**Files:**
- Create: `src/Orbital.App/Services/IAutoStartService.cs`
- Create: `src/Orbital.App/Services/WindowsAutoStartService.cs`
- Create: `src/Orbital.App/Services/MacAutoStartService.cs`
- Create: `src/Orbital.App/Services/LinuxAutoStartService.cs`
- Create: `src/Orbital.App/Services/AutoStartServiceFactory.cs`
- Modify: `src/Orbital.App/App.axaml.cs`

- [ ] **Step 1: Interface**

```csharp
// src/Orbital.App/Services/IAutoStartService.cs
namespace Orbital.App.Services;

public interface IAutoStartService
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}
```

- [ ] **Step 2: Windows impl**

```csharp
// src/Orbital.App/Services/WindowsAutoStartService.cs
namespace Orbital.App.Services;

using Microsoft.Win32;

public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Orbital";

    private readonly string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("ProcessPath null");

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                     ?? Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, $"\"{exePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
```

Add package `Microsoft.Win32.Registry` to `Orbital.App`:
```bash
dotnet add src/Orbital.App/Orbital.App.csproj package Microsoft.Win32.Registry --version 5.0.0
```

(On .NET 10 this is present in the BCL but the package works without issue. If compile complains about type not found on non-Windows build, wrap `WindowsAutoStartService` in `[SupportedOSPlatform("windows")]`.)

- [ ] **Step 3: macOS impl (LaunchAgent)**

```csharp
// src/Orbital.App/Services/MacAutoStartService.cs
namespace Orbital.App.Services;

using System.Diagnostics;
using System.IO;

public sealed class MacAutoStartService : IAutoStartService
{
    private static readonly string LaunchAgentsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
    private const string Label = "dev.orbital.app";
    private string PlistPath => Path.Combine(LaunchAgentsDir, Label + ".plist");

    private readonly string exePath = Environment.ProcessPath ?? "";

    public bool IsEnabled => File.Exists(PlistPath);

    public void Enable()
    {
        Directory.CreateDirectory(LaunchAgentsDir);
        var plist =
$@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key><string>{Label}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{exePath}</string>
    </array>
    <key>RunAtLoad</key><true/>
    <key>LimitLoadToSessionType</key><string>Aqua</string>
</dict>
</plist>";
        File.WriteAllText(PlistPath, plist);
        Run("launchctl", $"load -w \"{PlistPath}\"");
    }

    public void Disable()
    {
        if (File.Exists(PlistPath))
            Run("launchctl", $"unload -w \"{PlistPath}\"");
        File.Delete(PlistPath);
    }

    private static void Run(string fileName, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(fileName, args) { UseShellExecute = false });
            p?.WaitForExit();
        }
        catch { /* swallow; user can recover via toggle */ }
    }
}
```

- [ ] **Step 4: Linux impl (XDG autostart)**

```csharp
// src/Orbital.App/Services/LinuxAutoStartService.cs
namespace Orbital.App.Services;

using System.IO;

public sealed class LinuxAutoStartService : IAutoStartService
{
    private static readonly string AutostartDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
    private const string Filename = "orbital.desktop";
    private string FilePath => Path.Combine(AutostartDir, Filename);

    private readonly string exePath = Environment.ProcessPath ?? "";

    public bool IsEnabled => File.Exists(FilePath);

    public void Enable()
    {
        Directory.CreateDirectory(AutostartDir);
        var content =
$@"[Desktop Entry]
Type=Application
Name=Orbital
Exec={exePath}
X-GNOME-Autostart-enabled=true
Hidden=false
Terminal=false
";
        File.WriteAllText(FilePath, content);
    }

    public void Disable()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
```

- [ ] **Step 5: Factory**

```csharp
// src/Orbital.App/Services/AutoStartServiceFactory.cs
namespace Orbital.App.Services;

using System.Runtime.InteropServices;

public static class AutoStartServiceFactory
{
    public static IAutoStartService Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsAutoStartService();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     return new MacAutoStartService();
        return new LinuxAutoStartService();
    }
}
```

- [ ] **Step 6: Apply on settings save**

In `App.axaml.cs`, add field `private readonly IAutoStartService autoStart = AutoStartServiceFactory.Create();`

In `ShowSettings` (or add a `Host.SettingsChanged` handler):

```csharp
Host.SettingsChanged += () =>
{
    if (Host.Settings.StartAtLogin && !autoStart.IsEnabled) autoStart.Enable();
    else if (!Host.Settings.StartAtLogin && autoStart.IsEnabled) autoStart.Disable();
};
```

And on first boot, sync the flag to the current OS state (so fresh launch after manual removal shows the correct state):

```csharp
// after Host.LoadAsync:
if (Host.Settings.StartAtLogin != autoStart.IsEnabled)
{
    Host.Settings.StartAtLogin = autoStart.IsEnabled;
    await Host.SaveSettingsAsync();
}
```

- [ ] **Step 7: Manual verification**

Run the app, open Settings, enable "Start at login", save. On macOS: `ls ~/Library/LaunchAgents/dev.orbital.app.plist` — file exists. Log out/in: app should start in menu bar. Disable in Settings: plist removed and `launchctl unload` run.

- [ ] **Step 8: Commit**

```bash
git add src/Orbital.App/Services/ src/Orbital.App/App.axaml.cs src/Orbital.App/Orbital.App.csproj
git commit -m "Add per-OS auto-start services with factory + settings integration"
```

---

## Phase 9 — Packaging

---

### Task 28: Windows publish script

**Files:**
- Create: `build/publish-win.ps1`

- [ ] **Step 1: Script**

```powershell
# build/publish-win.ps1
param(
  [string]$Configuration = "Release",
  [string]$OutputDir = "publish/win-x64"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root
try {
    dotnet publish src/Orbital.App/Orbital.App.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $OutputDir

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

    $zip = "$OutputDir/../Orbital-win-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path "$OutputDir/*" -DestinationPath $zip
    Write-Host "Published to $zip"
}
finally {
    Pop-Location
}
```

- [ ] **Step 2: Commit** (no run — requires Windows)

```bash
git add build/publish-win.ps1
git commit -m "Add Windows publish script producing self-contained single-file zip"
```

---

### Task 29: macOS publish script with bundle

**Files:**
- Create: `build/publish-mac.sh`
- Create: `build/macos/Info.plist.template`
- Create: `build/macos/AppIcon.icns` (optional; placeholder accepted)

- [ ] **Step 1: `Info.plist.template`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key><string>Orbital.App</string>
    <key>CFBundleIdentifier</key><string>dev.orbital.app</string>
    <key>CFBundleName</key><string>Orbital</string>
    <key>CFBundleDisplayName</key><string>Orbital</string>
    <key>CFBundleVersion</key><string>0.1.0</string>
    <key>CFBundleShortVersionString</key><string>0.1.0</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>LSUIElement</key><true/>
    <key>LSMinimumSystemVersion</key><string>13.0</string>
</dict>
</plist>
```

- [ ] **Step 2: `publish-mac.sh`**

```bash
#!/usr/bin/env bash
# build/publish-mac.sh
set -euo pipefail

CONFIG="${CONFIG:-Release}"
RID="${RID:-osx-arm64}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/publish/$RID"
BUNDLE="$OUT/Orbital.app"

rm -rf "$BUNDLE"

dotnet publish "$ROOT/src/Orbital.App/Orbital.App.csproj" \
    -c "$CONFIG" \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -o "$OUT/payload"

mkdir -p "$BUNDLE/Contents/MacOS"
mkdir -p "$BUNDLE/Contents/Resources"

cp -R "$OUT/payload/"* "$BUNDLE/Contents/MacOS/"
cp "$ROOT/build/macos/Info.plist.template" "$BUNDLE/Contents/Info.plist"

# Optional icon
if [ -f "$ROOT/build/macos/AppIcon.icns" ]; then
  cp "$ROOT/build/macos/AppIcon.icns" "$BUNDLE/Contents/Resources/AppIcon.icns"
fi

# Make the main binary executable
chmod +x "$BUNDLE/Contents/MacOS/Orbital.App"

ZIP="$OUT/Orbital-macOS-${RID#osx-}.zip"
rm -f "$ZIP"
( cd "$OUT" && zip -r "$(basename "$ZIP")" "Orbital.app" > /dev/null )

echo "Published: $ZIP"
```

Make it executable: `chmod +x build/publish-mac.sh`

- [ ] **Step 3: Test the script**

Run: `./build/publish-mac.sh`

Expected: a `publish/osx-arm64/Orbital.app` bundle and an `Orbital-macOS-arm64.zip`. Open the `.app` (right-click → Open first time due to unsigned); tray icon should appear in menu bar.

- [ ] **Step 4: Commit**

```bash
chmod +x build/publish-mac.sh
git add build/publish-mac.sh build/macos/Info.plist.template
git commit -m "Add macOS publish script producing LSUIElement .app bundle"
```

---

### Task 30: Convenience wrapper

**Files:**
- Create: `build/build.sh`

- [ ] **Step 1: Wrapper**

```bash
#!/usr/bin/env bash
# build/build.sh — dispatch to the right platform script
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"

case "$(uname -s)" in
    Darwin*) "$DIR/publish-mac.sh" ;;
    Linux*)  echo "Linux build: run 'dotnet publish -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux-x64' manually"; exit 1 ;;
    MINGW*|MSYS*|CYGWIN*)
        echo "Run build/publish-win.ps1 from PowerShell instead"; exit 1 ;;
    *) echo "Unknown platform"; exit 1 ;;
esac
```

- [ ] **Step 2: Commit**

```bash
chmod +x build/build.sh
git add build/build.sh
git commit -m "Add build.sh wrapper dispatching to per-OS publish script"
```

---

## Phase 10 — Final polish and smoke test

---

### Task 31: Cross-platform smoke test checklist

**Files:** — no code; checklist verification

- [ ] **Step 1: Build Release on macOS**

```bash
dotnet build -c Release
dotnet test  -c Release
./build/publish-mac.sh
```

All three must succeed with 0 warnings and all tests passing.

- [ ] **Step 2: Launch the built `.app` and walk the scenarios**

1. Launch `Orbital.app` — tray icon shows in menu bar.
2. Press `Ctrl+Alt+T` — quick-add popup appears (grant Accessibility if prompted and relaunch).
3. Type "Buy milk" + Tab + "tomorrow" + Enter — popup closes, preview showed "→ Fri, ...".
4. Press `Ctrl+Alt+L` — overlay appears top-right with "Buy milk" row and due chip.
5. Add 2 more items via quick-add (different due dates, one empty).
6. In overlay: drag the third to the top. Restart the app; order persists.
7. Mark one complete via the checkbox; it disappears (`show ✓` off).
8. Toggle `show ✓`; the completed one reappears with strikethrough.
9. Select a row, press Del; toast-like "undo" lives via Ctrl+Z within the same open-overlay session.
10. Open Settings, change quick-add to `Ctrl+Alt+N`, Save; the new binding works without restart.
11. Toggle "Start at login"; verify plist exists; toggle off; verify plist removed.
12. Quit from the tray; relaunch; all data persisted in `todos.json`.

Any failure here is a real bug — open a fix task for it rather than skipping.

- [ ] **Step 3: (Optional) Windows verification**

If Windows is available:

```powershell
pwsh build/publish-win.ps1
```

Walk the same scenarios. Expect identical behavior; the system tray icon will be in the hidden-icons area unless the user pins it.

- [ ] **Step 4: Commit any smoke-test fixes**

If fixes were needed, commit them with descriptive messages. If not:

```bash
git status
# clean working tree — no commit needed
```

---

## Self-review summary (for the plan author, recorded here for transparency)

This plan was checked against `docs/superpowers/specs/2026-04-23-orbital-todo-design.md`:

- **§3 Tech stack:** tasks 1, 4 install the full stack.
- **§4 Solution structure:** tasks 1, 2 produce it exactly.
- **§5 Data model:** task 5.
- **§6 Storage:** tasks 6, 8, 9.
- **§7 Date parser:** task 7.
- **§8 Hotkeys / tray / autostart / lifecycle:** tasks 12, 13, 15, 27; macOS `LSUIElement` covered in task 29.
- **§9 Quick-add window:** tasks 16, 17, 18.
- **§10 Overlay window:** tasks 19, 20, 21, 22, 23, 24.
- **§11 Settings window:** tasks 25, 26.
- **§12 Packaging:** tasks 28, 29, 30.
- **§13 Scaffolding deliverables:** tasks 1, 2, 3.
- **§14 Testing approach:** tasks 5–10, 16, 19, 20 cover all table-driven Core tests; UI smoke in task 31.

Open items deliberately deferred to match spec §15 non-goals: hotkey conflict detection, recurring todos, search/filter, multi-device sync, signing/notarization.
