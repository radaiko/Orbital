# Orbital 1.0.1 — UI Refresh Design Spec

**Date:** 2026-04-23
**Status:** Draft, pending implementation
**Supersedes-nothing** — builds on MVP and 1.0 specs.

## 1. Purpose

Make Orbital 1.0 look unified, modern, minimalistic. The functional app is solid; the in-app look is inconsistent with the landing page, relies on platform-dependent emoji rendering for icons, and has specific layout clipping on header buttons. This spec defines:

- A unified palette + typography shared by all in-app windows and the landing page.
- A replacement of every emoji/Unicode glyph with properly-sized `Path`-based icons.
- Component-level styles reusable across windows.
- Refreshed layouts for the two user-visible windows (overlay, quick-add) and lighter touch-ups for the three infrequent windows (settings, about, message).
- A tray-icon left-click behaviour change: left-click opens the same menu that right-click opens on Windows.

## 2. Non-goals

- New features. No new actions, modes, keybindings, settings, or behaviors.
- Animation beyond what Avalonia provides for free (hover states).
- Light-mode parity at pixel-level — dark is canonical; light is "good enough".
- Landing-page changes (it already matches the target aesthetic).

## 3. Palette

Exact hex values, identical in XAML and in `docs/index.html`.

### Dark (primary)

| Token | Hex | Used for |
|---|---|---|
| `surface-0` | `#0A0A0A` | Window backgrounds (translucent over system blur) |
| `surface-1` | `#141414` | Cards, chips, text-box backgrounds |
| `surface-2` | `#1E1E1E` | Hover and selected rows |
| `border` | `#262626` | Window borders, card separators |
| `text` | `#FAFAFA` | Primary text |
| `text-dim` | `#888888` | Secondary text, placeholders, timestamps |
| `text-muted` | `#555555` | Tertiary text, disabled |
| `accent` | `#3B82F6` | Due-today chip, update-available, focus ring, selection indicator |
| `accent-hov` | `#60A5FA` | Hover on accent |
| `warning` | `#F59E0B` | Overdue chip, warning dialog |
| `danger` | `#EF4444` | Destructive action confirm (reserved; delete dialog only) |

### Light (inverted + tuned)

| Token | Hex |
|---|---|
| `surface-0` | `#FAFAFA` |
| `surface-1` | `#F0F0F0` |
| `surface-2` | `#E4E4E4` |
| `border` | `#D4D4D4` |
| `text` | `#1A1A1A` |
| `text-dim` | `#666666` |
| `text-muted` | `#999999` |
| `accent` | `#2563EB` |
| `accent-hov` | `#3B82F6` |
| `warning` | `#D97706` |
| `danger` | `#DC2626` |

### Brush resource wiring

`App.axaml` already declares a `ThemeDictionaries` block with 5 brushes:
`OrbitalOverlayBackgroundBrush`, `OrbitalOverlayBorderBrush`, `OrbitalOverlayForegroundBrush`, `OrbitalOverlayDimBrush`, `OrbitalOverlayChipBackgroundBrush`.

Keep those keys (no re-binding downstream); update the `Color` to match the new palette. Add seven new brushes:

- `OrbitalSurface1Brush`
- `OrbitalSurface2Brush`
- `OrbitalTextMutedBrush`
- `OrbitalAccentBrush`
- `OrbitalAccentHoverBrush`
- `OrbitalWarningBrush`
- `OrbitalDangerBrush`

All declared in both Dark and Light `ResourceDictionary` entries.

## 4. Typography

### Font

Inter, bundled via Avalonia's `.WithInterFont()` (already configured in `Program.BuildAvaloniaApp`). No change required.

### Size scale

| Token | Size | Weight | Use |
|---|---|---|---|
| `caption` | 11 | Regular | Chips, footer stats, preview-row text |
| `body` | 13 | Regular | List rows, fields, menu items |
| `body-emph` | 13 | SemiBold | Row title when emphasis needed |
| `subtitle` | 14 | SemiBold | Overlay header, Settings tab titles |
| `title` | 16 | SemiBold | About app-name, MessageWindow title |
| `heading` | 20 | SemiBold | Reserved (not used in 1.0.1) |

### Style rules

- **Every `<TextBlock>` gets an explicit `FontSize`.** Default 14 is the root cause of the current header-button clipping; removing reliance on defaults eliminates regressions when Avalonia updates its FluentTheme defaults.
- `LetterSpacing="-0.01em"` on subtitle and above (matches the landing page).
- `LineHeight` 1.4 for body text; 1.2 for headings.

## 5. Icon system

### Source

[Lucide](https://lucide.dev) (MIT). Each icon ships as an SVG path; we embed the path's `d` attribute verbatim in a `StreamGeometry` resource.

### Resource dictionary

`src/Orbital.App/Icons.axaml` (new merged dictionary loaded by `App.axaml`):

The dictionary defines one `StreamGeometry` per icon key below. The `d`-attribute content is pulled verbatim from each corresponding file in the Lucide repository (e.g. `lucide/icons/settings.svg`). Example for two of the simpler icons that can be written in full:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StreamGeometry x:Key="Icon.Close">M18 6L6 18M6 6l12 12</StreamGeometry>
    <StreamGeometry x:Key="Icon.Check">M20 6L9 17l-5-5</StreamGeometry>
    <!-- Icon.Settings, Icon.ArrowUp, Icon.AlertTriangle, Icon.Calendar,
         Icon.GripVertical, Icon.ChevronDown, Icon.Trash, Icon.ExternalLink,
         Icon.ArrowRight — d-attribute copied from the corresponding
         file in lucide/icons/*.svg at implementation time. -->
</ResourceDictionary>
```

Each geometry is a single stroked path with stroke-width 2, stroke-linecap round, stroke-linejoin round (Lucide's defaults). ViewBox is `0 0 24 24` for all Lucide icons; we set `Stretch="Uniform"` on the `Path` so the 24-unit space scales to whatever `Width`/`Height` the consumer specifies.

### Rendering

`Path` or `PathIcon`:

```xml
<Path Data="{StaticResource Icon.Settings}"
      Width="16" Height="16"
      Stretch="Uniform"
      Stroke="{Binding $parent[Button].Foreground}"
      StrokeThickness="2"
      StrokeLineCap="Round"
      StrokeJoin="Round"
      Fill="Transparent" />
```

Since Lucide icons are stroke-based (not filled), we render via `Stroke` + `StrokeThickness`. An `IconPresenter` content control wrapper could simplify consumer code; that's an implementation detail.

### Size rules

| Context | Icon size |
|---|---|
| Inline with body text | 14 |
| Button content | 16 |
| Window header | 20 |
| Empty state | 32 |

### Emoji removal map

| Current | Replacement |
|---|---|
| `⚙︎` (settings button) | `Icon.Settings` 16px |
| `✕` (close button) | `Icon.Close` 16px |
| `✓` in "show ✓" ToggleButton | `Icon.Check` 14px inline |
| `✓` in "✓ Up to date" tray item | `Icon.Check` 16px |
| `⬆` in "⬆ Update to vX" tray item | `Icon.ArrowUp` 16px |
| `⚠` in "⚠ Grant Accessibility…" tray item | `Icon.AlertTriangle` 16px |
| `⚠` in overdue chip | `Icon.AlertTriangle` 14px inline |
| `→` in quick-add preview | Keep (single Unicode arrow, non-emoji, renders consistently) OR `Icon.ArrowRight` 14px — pick one at implementation |
| `—` (em-dash for "no due") | Keep (typography character, not emoji) |

## 6. Component styles

Defined in `App.axaml` as `<Style Selector="...">`. Consumed by setting the control's `Classes` attribute.

### `Button.icon` — square icon button

```xml
<Style Selector="Button.icon">
    <Setter Property="Width" Value="28"/>
    <Setter Property="Height" Value="28"/>
    <Setter Property="Padding" Value="6"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="CornerRadius" Value="6"/>
    <Setter Property="Foreground" Value="{DynamicResource OrbitalOverlayDimBrush}"/>
</Style>

<Style Selector="Button.icon:pointerover">
    <Setter Property="Background" Value="{DynamicResource OrbitalSurface2Brush}"/>
    <Setter Property="Foreground" Value="{DynamicResource OrbitalOverlayForegroundBrush}"/>
</Style>

<Style Selector="Button.icon:focus-visible">
    <Setter Property="BorderBrush" Value="{DynamicResource OrbitalAccentBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
</Style>
```

### `Button.primary` — accent CTA

Height 32, padding 12,6, background `AccentBrush`, foreground pure white, hover `AccentHoverBrush`, corner radius 6, font body SemiBold.

### `Button.ghost` — secondary CTA

Height 32, padding 12,6, transparent bg, `TextDimBrush` text, hover `Surface2Brush` bg + `TextBrush`.

### `Border.chip` — due-date chip

Padding 8,2, corner radius 4, background `Surface1Brush`, foreground `TextDimBrush`, 11pt caption.

Variants via Classes:
- `.today` → `AccentBrush` bg + white fg
- `.overdue` → `WarningBrush` bg + white fg + inline `Icon.AlertTriangle` 12px

### `TextBox.field`

Border none, bg `Surface1Brush`, focus bg `Surface2Brush` + 1px `AccentBrush` inside border (not outside, so no layout jitter), padding 10,6, corner radius 6, placeholder color `TextMutedBrush`.

Applies automatically to all `TextBox` via default `Style Selector="TextBox"` in `App.axaml` — no Classes needed.

### `CheckBox` override

Default Avalonia checkbox is too tall for a 40px row. Custom `ControlTemplate`:
- 16×16 visible box
- `Surface1Brush` bg unchecked, `AccentBrush` bg checked
- 1px `TextDimBrush` border unchecked
- Checkmark is `Icon.Check` 12px pure white, centered
- Corner radius 4

Applies automatically via default `TextBox` and `CheckBox` style selectors — no Classes, no per-usage opt-in.

## 7. Layouts

### 7.1 Overlay window

**Size:** 380 × 520 (was 360 × 520). +20px width for chip breathing room.

**Layout:**

```
┌────────────────────────────────────────┐
│  Orbital                          [⚙][✕]│   44px header
│                                        │
├────────────────────────────────────────┤
│                                        │   8px gap
│  [ ]  Buy milk                tomorrow │   40px row
│  [ ]  File taxes              Apr 30 ⚠ │
│  [ ]  Finish design doc          Fri   │
│  [ ]  Read book                   —    │
│  ⋮                                     │   grip visible on row hover
│                                        │
├────────────────────────────────────────┤
│  4 open · 2 done today         ✓ show  │   36px footer
└────────────────────────────────────────┘
```

**Header:** 44px height. `Orbital` word-mark 14pt SemiBold, `-0.01em` tracking. Gear + close buttons are `Button.icon` 28×28 with 16px `Path` icons.

**Row:** `Height=40`, padding `12,0`. Checkbox 16×16 left, Title flex center, Chip right-aligned. Drag handle `Icon.GripVertical` 16px on the far left, opacity 0 → 0.6 on row hover (implemented via `Grid` with a 20px leading column that's invisible by default).

**Selection indicator:** 2px vertical `AccentBrush` strip at the row's left edge when `ListBoxItem.IsSelected`. Replaces Avalonia's default focus highlight.

**Empty state:** when `Rows.Count == 0`, a single centered `StackPanel`:
- `Icon.Check` 32px at 25% opacity
- `TextBlock` "Inbox zero." 14pt subtitle
- `TextBlock` "Hit ⌃⌥T to add a todo." 11pt caption, `TextDimBrush`

**Footer:** 36px. Count on left. "show" toggle on the right using a `ToggleButton.ghost` with inline `Icon.Check` 14px + caption "show" (not "show ✓").

**Acrylic:** `TransparencyLevelHint="AcrylicBlur"` kept. Background `surface-0` at ~80% alpha (`#CC0A0A0A`) over the blur. On Light: `#CCFAFAFA`.

### 7.2 Quick-add window

**Size:** 440 × 88 (was 420 × 72).

```
┌────────────────────────────────────────────────────┐
│  ▢ [ What needs doing? ____________ ] [ due… ]    │   44px input row
│     → Fri, Apr 24                                  │   24px preview row
└────────────────────────────────────────────────────┘
```

- Input row: Title 70% width, Due 25% width. Both use the new `TextBox` default style (filled `Surface1Brush`, focus accent border).
- Preview row: `Icon.ArrowRight` 14px + parsed date caption, `TextMutedBrush`. On parse error: `WarningBrush` foreground + `Icon.AlertTriangle` 14px + "didn't understand 'asdf'".

### 7.3 Settings window

**Size:** 480 × 420 (was 480 × 380). +40px vertical for easier scanning.

- Section headers: subtitle-size, `TextBrush`, 12px bottom margin, 20px top margin between sections.
- Field spacing: 12px vertical between field rows (was 8).
- Hotkey text fields: use new `TextBox` style.
- Overlay position: `ComboBox` default template swapped to use `Icon.ChevronDown` 14px caret.
- Checkboxes: new custom `CheckBox` template.
- Save button: `Button.primary`. Cancel: `Button.ghost`.

### 7.4 About window

**Size:** 400 × 360 (was 360 × 320). +40w, +40h for breathing room.

- App icon: render from `avares://Orbital.App/Assets/tray-icon-96.png` — a new 96×96 PNG generated from `icon.svg` (the main icon, not the bolder tray variant), added to generate-icons.sh output.
- App name: "Orbital" 20pt heading (first use of `heading`).
- Version: 11pt caption `TextDimBrush` directly below.
- "Check for updates" button: `Button.primary` 32px height.
- Status label below: 11pt caption.
- "GitHub" link: `Button.ghost` with inline `Icon.ExternalLink` 14px + "GitHub" text.
- Copyright: 11pt caption `TextMutedBrush`, centered.

### 7.5 MessageWindow

**Size:** 440 × 200 (unchanged).

- Title: 16pt title, `TextBrush`.
- Body: 13pt body, `TextDimBrush`, wrapped.
- Primary button: `Button.primary`. Cancel (if shown): `Button.ghost`.
- Corner radius 10.

## 8. Tray interaction and tray icon

### 8.1 Left-click opens the menu

Avalonia `TrayIcon` exposes a `Clicked` event that fires on left-click on all platforms. Currently unused. Wire it to show the same `NativeMenu` that right-click shows.

Mechanism differs by platform:

- **macOS:** menu bar icons already open their menu on any click — this is OS-native behavior. If Avalonia's default wiring doesn't do this, we call `trayIcon.Menu.Open()` (or equivalent) from the `Clicked` handler. If the OS shows it automatically already, the handler becomes a no-op and no harm.
- **Windows:** `TrayIcon.Clicked` fires for left-clicks. We call `Menu.Open(trayIcon)` or equivalent from the handler to show the same menu right-click already shows.

Implementation goes in `TrayIconController.Show()`:

```csharp
trayIcon.Clicked += (_, _) =>
{
    // Open the menu programmatically. Avalonia 12 does not auto-open on
    // left-click on Windows by default; on macOS this is a no-op because
    // the system already opens on click.
    // Exact API: check NativeMenu / TrayIcon docs for 12.x.
    trayIcon.Menu?.Open();
};
```

If no direct `Open()` method exists, fall back to emulating a right-click position (`OpenMenuAt(cursor position)`), or accept the feature as macOS-only and document the Windows limitation.

### 8.2 Tray icon @2x

`generate-icons.sh` produces a second PNG `tray-icon@2x.png` at 64×64 (rendered from `tray-icon.svg`). macOS picks up `@2x` variants automatically when the display is Retina. On Windows, DPI scaling handles it; no extra file needed there.

Adds two lines to `generate-icons.sh`:

```bash
rsvg-convert -w 64 -h 64 "$SCRIPT_DIR/tray-icon.svg" -o "$ROOT/src/Orbital.App/Assets/tray-icon@2x.png"
```

`TrayIconController.LoadIcon` loads `tray-icon.png` and relies on Avalonia/OS to pick up the `@2x` when appropriate.

## 9. Testing

- Manual smoke on macOS (primary target) + Windows if available:
  - Overlay: open, keyboard nav, inline edit, drag-reorder, hover states, empty state.
  - Quick-add: type, parse preview (success + error), submit, focus handling.
  - Settings: open, edit each field, save, cancel.
  - About: open, check-updates button, GitHub link click.
  - MessageWindow: triggered via a deliberate settings-save failure (e.g. chmod 000 on settings.json).
  - Tray: left-click (menu appears), right-click (same menu), hover (tooltip), update states.

- No unit tests are required for the UI refresh (XAML + style changes). The existing 81 tests continue to pass — any VM changes from this spec must not alter public behaviour (pure visual).

## 10. Version

Bump `Directory.Build.props` Version 1.0.0 → 1.0.1. Release via existing `./build/release.sh`. The Velopack-installed 1.0.0 on dev machines picks up 1.0.1 via the tray "Check for updates…" flow — this release is also the first real update-chain test.

## 11. Open questions / deferred

- **Icon set licensing notice**: add Lucide MIT attribution to `LICENSE` or README? Lucide's MIT license requires preservation in distributed source. Since the path `d` data is embedded in our MIT-licensed XAML, include a one-line credit in README "Icons from [Lucide](https://lucide.dev) (MIT)".
- **Light-mode CheckBox contrast**: the new custom template may need a manual tweak for light mode — verify visually.
- **IconPresenter wrapper control**: considered, deferred. Direct `<Path>` with ~6 attributes per usage is slightly verbose but doesn't justify a new control in a small app.
- **Animated state transitions**: row fade-out on complete is already there (200ms). No other animations added.
