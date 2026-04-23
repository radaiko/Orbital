# Orbital — Design Spec

**Date:** 2026-04-23
**Status:** Draft, pending implementation

## 1. Purpose

A fast, local, keyboard-driven personal todo list for Windows and macOS (with Linux as a free bonus from the stack). Two global hotkeys drive the whole experience: one summons a quick-add popup, the other toggles a translucent always-on-top overlay. No priority field — ordering is manual drag-and-drop, with due date as display context. No account, no sync, no server.

## 2. Non-goals (for MVP)

- Multi-device sync (may come later via per-todo append log or real backend — out of scope).
- Priority / tags / categories / projects.
- Subtasks / dependencies.
- Times of day on due dates (day-precision only).
- Reminders / notifications.
- Mobile apps.
- Installer signing / notarization (packaging MVP ships unsigned).

## 3. Tech stack

- **Language / runtime**: C# on .NET 10 (LTS).
- **UI**: Avalonia 12 (latest stable).
- **MVVM**: CommunityToolkit.Mvvm (source generators).
- **Global hotkeys**: `SharpHook` (cross-platform low-level key hook).
- **Storage**: JSON via `System.Text.Json`.
- **Testing**: xUnit + FluentAssertions.

Rationale: Avalonia + .NET gives real Windows and macOS support from a single codebase with sensible native-ish behavior (acrylic/mica, native tray, native windowing). Avoiding EF/SQLite keeps the app simple and makes the on-disk data friendly to any future cloud-drive or manual-merge sync approach.

## 4. Solution structure

```
Orbital/
├── Orbital.sln
├── Directory.Build.props
├── .editorconfig
├── .gitignore
├── README.md
├── CLAUDE.md
├── docs/
│   └── superpowers/specs/2026-04-23-orbital-todo-design.md   (this file)
├── build/
│   ├── publish-win.ps1
│   ├── publish-mac.sh
│   └── build.sh
├── src/
│   ├── Orbital.Core/
│   │   ├── Models/
│   │   │   ├── Todo.cs
│   │   │   └── AppSettings.cs
│   │   ├── Persistence/
│   │   │   ├── ITodoStore.cs
│   │   │   ├── JsonTodoStore.cs
│   │   │   ├── ISettingsStore.cs
│   │   │   ├── JsonSettingsStore.cs
│   │   │   └── AppPaths.cs
│   │   └── DateParsing/
│   │       ├── DueDateParser.cs
│   │       └── ParseResult.cs
│   └── Orbital.App/
│       ├── App.axaml(.cs)
│       ├── Program.cs
│       ├── ViewModels/
│       │   ├── QuickAddViewModel.cs
│       │   ├── OverlayViewModel.cs
│       │   ├── TodoRowViewModel.cs
│       │   └── SettingsViewModel.cs
│       ├── Views/
│       │   ├── QuickAddWindow.axaml
│       │   ├── OverlayWindow.axaml
│       │   └── SettingsWindow.axaml
│       └── Services/
│           ├── IGlobalHotkeyService.cs
│           ├── SharpHookGlobalHotkeyService.cs
│           ├── IAutoStartService.cs
│           ├── WindowsAutoStartService.cs
│           ├── MacAutoStartService.cs
│           ├── LinuxAutoStartService.cs
│           └── TrayIconController.cs
└── tests/
    └── Orbital.Core.Tests/
        ├── DueDateParserTests.cs
        ├── JsonTodoStoreTests.cs
        └── OrderingTests.cs
```

### Project split (Approach 2)

- **`Orbital.Core`**: pure .NET library. Models, storage, date parser. No UI, no platform APIs. Testable in isolation.
- **`Orbital.App`**: Avalonia UI + all platform glue (hotkeys, tray, autostart).
- **`Orbital.Core.Tests`**: xUnit.

## 5. Data model

### `Todo`

```csharp
public sealed record Todo
{
    public required Guid Id { get; init; }
    public required string Title { get; set; }
    public DateOnly? DueDate { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }   // null = active
    public required int Order { get; set; }            // manual sort; lower = higher in list
}
```

- `DateOnly` for due date: day-precision matches the parser and UI; avoids TZ pain.
- `CompletedAt` as timestamp (not bool) gives future "recently completed" queries for free.
- `Order` is a simple int reassigned on reorder. Rebalancing is cheap at our scale.

### `AppSettings`

```csharp
public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public HotkeyBinding QuickAddHotkey { get; set; } = new(KeyModifiers.Control | KeyModifiers.Alt, Key.T);
    public HotkeyBinding ToggleOverlayHotkey { get; set; } = new(KeyModifiers.Control | KeyModifiers.Alt, Key.L);
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopRight;
    public bool OverlayAutoHideOnFocusLoss { get; set; } = true;
    public bool ShowCompleted { get; set; } = false;
    public bool StartAtLogin { get; set; } = false;
}

public sealed record HotkeyBinding(KeyModifiers Modifiers, Key Key);
public enum OverlayPosition { TopRight, TopLeft, BottomRight, BottomLeft }
```

The default uses `Ctrl+Alt` literally on both platforms — on macOS that's Control+Option (`⌃⌥`), not Command. This avoids the usual cross-platform modifier translation tangle: the same modifier constants and key codes work identically everywhere, and SharpHook's raw key events match what users type. Users who prefer Cmd-based combos on macOS can rebind in Settings.

## 6. Storage

### Paths

- **Windows**: `%APPDATA%\Orbital\`
- **macOS**: `~/Library/Application Support/Orbital/`
- **Linux**: `$XDG_DATA_HOME/Orbital/` (fallback `~/.local/share/Orbital/`)

Files: `todos.json`, `settings.json`.

### `todos.json` format

```json
{
  "schemaVersion": 1,
  "todos": [
    {
      "id": "3c1c…",
      "title": "Buy milk",
      "dueDate": "2026-04-24",
      "createdAt": "2026-04-23T09:12:00+02:00",
      "completedAt": null,
      "order": 0
    }
  ]
}
```

### Write strategy — atomic, crash-safe

1. Serialize to `todos.json.tmp` in the same directory.
2. `fsync` (via `FileStream.Flush(flushToDisk: true)`).
3. `File.Move(tmp, final, overwrite: true)` — atomic rename on NTFS and APFS.

Writes are **debounced 250 ms** after mutations so drag-reorder bursts don't thrash the disk.

### `ITodoStore`

```csharp
public interface ITodoStore
{
    Task<IReadOnlyList<Todo>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IReadOnlyList<Todo> todos, CancellationToken ct = default);
}
```

Load-all / save-all. Sufficient at <1k items.

### Concurrency

Single-process only. A named mutex (`Global\Orbital.SingleInstance`) on Windows and a `.pid` lockfile with `flock` on macOS/Linux prevent two simultaneous instances. Second launch exits silently.

## 7. Date parser

Grammar (order of precedence; first match wins):

1. Empty input → no due date.
2. **ISO**: `2026-04-25`, `2026/04/25`.
3. **Short D.M / D/M / D-M** (locale-aware): `25.4`, `25/4`, `25-04`. Assumes current year; if the resulting date is already past, bumps to next year.
4. **Relative**: `today`, `tomorrow`, `tmr`, `in N days`, `in N weeks`.
5. **Weekday**: `mon` .. `sunday` — resolves to the next occurrence on or after today. Typing "mon" on a Monday means today.
6. **"next X"**: `next week` → next Monday; `next month` → same day-of-month next month (clamped); `next mon` → Monday after next.

Hand-rolled, tokenized, no external NLP. All rules covered by a table-driven test in `Orbital.Core.Tests/DueDateParserTests.cs`.

## 8. Global hotkeys & platform integration

### Hotkey service

```csharp
public interface IGlobalHotkeyService : IAsyncDisposable
{
    Task StartAsync();
    IDisposable Register(HotkeyBinding binding, Action onPressed);
}
```

Backed by `SharpHook` — single low-level global key hook, dispatches matching chords. Callbacks are marshalled onto the UI thread via `Dispatcher.UIThread.Post`.

**macOS caveat**: requires Accessibility permission. On denied permission we show a small tray menu banner explaining how to enable it in System Settings → Privacy & Security → Accessibility.

### Default hotkeys

- Quick-add: `Ctrl+Alt+T` (Windows/Linux), `⌃⌥T` (macOS).
- Toggle overlay: `Ctrl+Alt+L` (Windows/Linux), `⌃⌥L` (macOS).

Fully user-configurable in Settings.

### Tray icon / menu-bar

Avalonia 12 native `TrayIcon`. Menu:

```
Orbital
─────────────
Quick add                ⌃⌥T
Show overlay             ⌃⌥L
─────────────
Settings…
Open data folder
Start at login           ☐
─────────────
Quit
```

### Autostart

- **Windows**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- **macOS**: LaunchAgent plist at `~/Library/LaunchAgents/dev.orbital.app.plist` (`RunAtLoad=true`, `LimitLoadToSessionType=Aqua`). `launchctl load/unload` on toggle.
- **Linux**: `.desktop` file in `~/.config/autostart/`.

Off by default. User opts in from tray menu or settings.

### Lifecycle

- `Orbital.App` is an Avalonia app that starts with **no main window** — just tray icon and hotkey service.
- Quick-add and overlay windows are created lazily and then reused (hidden, not destroyed) for instant re-show.
- On quit: save settings, dispose hotkey hook, remove tray icon, exit.

### macOS specifics

`Info.plist` must set `LSUIElement = true` so the app has no Dock icon. Bundle ID: `dev.orbital.app`.

## 9. Quick-add window

- Frameless, ~420×72 px, centered on active monitor at ~⅓ from top.
- Always-on-top while visible; hides on Esc, Enter (submit), or focus loss.
- Layout: Title (70%) → Due (25%) → calendar-picker icon. Tab cycles fields. Autofocus on Title.
- Live parse preview under the due-date field; red label if unparseable, Enter blocked.
- Re-invoking the hotkey while visible: focus + select title, don't reset typed content.
- Empty title + Enter: no-op (silent).

New todos prepend (top of list), `Order = min(existing) - 1`, or `0` if the list is empty.

## 10. Overlay window

### Appearance

- Frameless, rounded corners, drop shadow.
- Translucent blurred background. Windows: `TransparencyLevelHint = AcrylicBlur`. macOS: `Mica` / `AcrylicBlur`.
- ~360×520 px.
- Position: default top-right with 24 px screen margin; configurable (TopRight / TopLeft / BottomRight / BottomLeft).
- Shown on the currently-focused monitor.
- Always-on-top.
- Hides on Esc, hotkey again, or focus loss (focus-loss behavior is configurable).

### Layout

```
┌────────────────────────────────────┐
│  Orbital                  ⚙︎   ✕  │   header (drag to move)
├────────────────────────────────────┤
│  ☐  Buy milk            tomorrow   │
│  ☐  File taxes           Apr 30 ⚠  │
│  ☐  Finish design doc    Fri       │
│  ☐  Read book            —         │
├────────────────────────────────────┤
│  4 open · 2 done today   [show ✓] │   footer
└────────────────────────────────────┘
```

### Row interactions

- Checkbox click → toggle complete; if completing and `ShowCompleted=false`, row animates out (~200 ms fade).
- Double-click row or Enter when selected → inline edit title.
- Chevron next to due date → inline edit due date (same parser).
- Drag handle (6-dot icon, appears on hover) → reorder. Visual insertion line between rows.
- `Del` → delete with 5s undo toast.
- `S` → snooze (+1 day to due; creates "tomorrow" if none).
- `↑` / `↓` → move selection.
- `Space` → toggle complete on selected row.
- `Ctrl/Cmd+Z` → undo last destructive action (single-level stack).
- `Ctrl/Cmd+N` → open quick-add.

### Sorting

Active todos: sorted by `Order` ascending. Only manual drag reorders items. Quick-add new items get `Order = min(existing) - 1` so they land on top.

Completed todos (when visible): below all active items, sorted by `CompletedAt` descending.

### Overdue rendering

- Due today → amber chip.
- Due in past → red chip + `⚠` icon.
- No due → em-dash in grey.

## 11. Settings window

Tabbed single window, saved live to `settings.json`.

- **Hotkeys**: capture-and-display for Quick-add and Toggle-overlay. "Press a combination" recording state. Rejects empty / modifier-only bindings.
- **Overlay**: position, auto-hide on focus loss, show completed.
- **Startup**: start at login.
- **Data**: open data folder; reset all data (confirmation, writes `todos.backup.json` first).

## 12. Packaging

- **Windows**: `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`. Zip distribution for MVP.
- **macOS**: `.app` bundle with custom `Info.plist`. Published for `osx-arm64` (default) and `osx-x64` on demand. Unsigned for MVP — user right-clicks → Open on first run.

Scripts under `build/` wrap these.

## 13. Project scaffolding deliverables

- `README.md` — purpose, install (Win + mac), default hotkeys, data locations, macOS Accessibility permission walkthrough, troubleshooting.
- `CLAUDE.md` — conventions: project structure pointer, C# style (nullable on, file-scoped namespaces, analyzers as errors), testing expectation (`dotnet test` before claiming done), packaging targets, known platform quirks.
- `.gitignore` — .NET, Avalonia, macOS, IDE artifacts.
- `.editorconfig` — 4-space C#, LF, file-scoped namespaces, nullable enabled.
- `Directory.Build.props` — shared `TargetFramework=net10.0`, `Nullable=enable`, `LangVersion=latest`, warnings-as-errors for analyzers.
- `Orbital.sln` + three csproj files.

## 14. Testing

`Orbital.Core.Tests`:

- **`DueDateParserTests`** — table-driven, ~30 cases covering every rule, plus unparseables.
- **`JsonTodoStoreTests`** — round-trip, atomic write (simulated crash via interrupted temp-file rename), schema migration stub.
- **`OrderingTests`** — reorder, prepend-new, stable across completion toggles.

UI is not unit-tested for MVP; smoke-tested by hand on both platforms.

CI (later, optional): GitHub Actions running `dotnet test` on `ubuntu-latest`.

## 15. Open questions / deferred

- **Hotkey conflict detection**: post-MVP — OS-level conflict detection is hard. For MVP we rely on user config.
- **Recurring todos** (daily, weekly) — deferred.
- **Search / filter** — deferred; JSON + small list makes scroll sufficient.
- **Multi-device sync** — deferred; JSON keeps this door open.
- **Signing / notarization** — deferred; MVP ships unsigned.
