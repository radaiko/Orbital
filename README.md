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
