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
