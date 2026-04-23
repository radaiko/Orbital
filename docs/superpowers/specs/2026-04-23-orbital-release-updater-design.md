# Orbital 1.0 — Release, Updater, Landing Page Design Spec

**Date:** 2026-04-23
**Status:** Draft, pending implementation
**Supersedes-nothing** — builds on `2026-04-23-orbital-todo-design.md` (the MVP spec).

## 1. Purpose

Take the in-repo MVP of Orbital (tray-resident keyboard-driven todo list) from "runnable from a `dotnet run`" to a polished shippable 1.0: signed and notarized macOS bundle, portable Windows bundle, single-command local release pipeline, Velopack-backed in-app updates, and a GitHub Pages landing page for discoverability.

## 2. Non-goals (still)

- Windows code signing — omitted intentionally. Users accept one-time SmartScreen prompt.
- Linux first-class distribution — still a bonus (build from source only).
- Multi-channel releases (beta, nightly) — stable only.
- Auto-publishing to the Mac App Store / Microsoft Store.
- Reminders, sync, search — still deferred per MVP spec §15.

## 3. Scope overview

Five work areas, implemented in order:

1. **Polish** — app icon, version handling, About dialog, logging, error dialogs, macOS Accessibility flow, theme correctness, first-run welcome todo.
2. **Branding pipeline** — a single SVG source that generates every raster/icns/ico artifact used by app, bundle, landing page, release.
3. **Release pipeline** — `build/release.sh` orchestrating build, sign, notarize, Velopack pack, and GitHub release publish.
4. **Velopack updater integration** — in-app update check using GitHub Releases as the update source.
5. **Landing page** — `docs/` folder served by GitHub Pages, visually cloning the nook-app design.

## 4. Polish items (Area 1)

### 4.1 Real app icon
- Geometric, minimal: filled rounded square + stylized orbit ring, blue-on-dark.
- Source of truth: `build/branding/icon.svg`.
- Derived via `build/branding/generate-icons.sh`:
  - `build/branding/AppIcon.icns` (macOS, via `iconutil` from generated PNG set sizes 16–1024)
  - `build/branding/AppIcon.ico` (Windows, via `png2ico` or `convert`, sizes 16/32/48/256)
  - `src/Orbital.App/Assets/tray-icon.png` (32×32)
  - `docs/icon.svg` (landing page)
  - `build/branding/icon-{128,256,512,1024}.png` (landing page, future store assets)
- The script is rerunnable idempotently; CI could run it to verify output matches committed files, but that is a later concern.

### 4.2 Versioning
- `Directory.Build.props` carries the authoritative version:
  ```xml
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  ```
- `release.sh` reads it via `dotnet msbuild -getProperty:Version`.
- Manual bumps only. `release.sh` refuses to run if the tag `v<version>` already exists on `origin`.
- `Info.plist.template` has its `CFBundleShortVersionString` and `CFBundleVersion` values replaced at build time using `sed`, keyed off the same version.

### 4.3 About dialog
- New `src/Orbital.App/Views/AboutWindow.axaml` + `AboutViewModel` (lives in `Orbital.Core.ViewModels`).
- Opened from tray menu "About Orbital…" (new menu item above Quit) and from Settings (bottom-right link "About").
- Shows:
  - App icon (from assets)
  - "Orbital" name
  - Version string read via `typeof(App).Assembly.GetName().Version?.ToString(3)`
  - "Check for updates" button (wired to `IUpdateService.CheckAsync`, shows inline status)
  - "GitHub repository" link → `https://github.com/radaiko/Orbital`
  - Copyright line
- Fixed size 360×280, not resizable, standard window chrome.

### 4.4 File logging
- Add `Microsoft.Extensions.Logging.Abstractions` + a lightweight file sink to `Orbital.App` only (keep `Orbital.Core` dep-free; the Core stays silent, the App does logging).
- Implementation: a small `FileLoggerProvider` writing plain text lines to `AppPaths.DataDirectory/logs/<yyyyMMdd_HHmmss>.log`, one file per session.
- Log rotation: on startup, keep last 10 files, delete older.
- Call sites migrate: every current `Debug.WriteLine` in `Orbital.App/**` becomes `logger.LogDebug/Info/Warning/Error`.
- No config UI for log level for MVP; default is `Information`. An env var `ORBITAL_LOG_LEVEL=Debug` overrides.

### 4.5 User-visible error dialogs
- New interface `IDialogService` in `Orbital.App/Services/`:
  ```csharp
  public interface IDialogService
  {
      Task ShowErrorAsync(string title, string message);
      Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "OK");
  }
  ```
- Avalonia implementation backed by a small `MessageWindow` that can be either modal or toast-style. MVP: modal.
- Used by:
  - `App.ShowSettings` Save error path (replaces the `Debug.WriteLine`-only fallback)
  - `VelopackUpdateService` update-failure path
  - The Accessibility-denied detection path (see 4.6)
  - Save failures from `AppHost.FlushAsync` on Quit (best-effort; don't block Quit on dialog close)

### 4.6 macOS Accessibility flow
- `SharpHookGlobalHotkeyService` already signals failures via the `ContinueWith(OnlyOnFaulted)` path.
- New state on `TrayIconController`: `public bool AccessibilityDenied { get; private set; }` + event `AccessibilityStateChanged`.
- When the hotkey service faults with an indication of permission denial (exception type or message heuristic — document in code), `App` sets `tray.AccessibilityDenied = true`.
- Tray menu gains a conditional top item when `AccessibilityDenied`:
  ```
  ⚠ Grant Accessibility…
  ─────────────
  (rest of menu)
  ```
- Click opens System Settings via `Process.Start(new ProcessStartInfo("open", "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility"))`. We do NOT prompt on every launch after denial — user decides when to fix it.
- We also surface one-time modal dialog on the first launch where denial is detected, explaining what to do.

### 4.7 Theme correctness
- `App.axaml` keeps `RequestedThemeVariant="Default"` so the app follows the OS.
- Revisit `QuickAddWindow`, `OverlayWindow` hard-coded colors (`#CC1E1E1E`, etc.) and replace with theme resource references:
  - Background: `{DynamicResource SystemAltHighColor}` or a custom brush declared in `App.axaml`.
  - Text: `{DynamicResource SystemBaseHighColor}`.
- Two custom resources in `App.axaml.Resources`:
  - `OrbitalOverlayBackgroundBrush` — dark: `#CC1E1E1E`; light: `#F5F5F5FF`.
  - `OrbitalOverlayForegroundBrush` — dark: white; light: near-black.
- Visually test both themes locally. Dark remains the canonical look.

### 4.8 First-run welcome todo
- `AppHost.LoadAsync` — after loading, if `Todos.Count == 0 AND settings.json did not previously exist`, insert a single todo:
  ```
  Title: "Welcome to Orbital — press Ctrl+Alt+L to open, Ctrl+Alt+T to add"
  DueDate: null
  Order: 0
  ```
- Detection of "settings.json did not previously exist" is done via a boolean returned from `JsonSettingsStore.LoadAsync` (new overload `(AppSettings, bool wasDefaulted)`).

## 5. Branding pipeline (Area 2)

### 5.1 Source SVG
- Square canvas 1024×1024.
- Rounded rect background fill `#0a0a0a` (matches landing-page bg).
- Orbit ring: stroked ellipse at ~30° tilt, stroke `#3b82f6`.
- Small dot on the orbit path (the "todo" node): fill `#60a5fa`.
- No text; scalable to 16×16 without losing recognizability.

### 5.2 `generate-icons.sh`
- Dependencies: `rsvg-convert` (macOS: `brew install librsvg`) for SVG→PNG, `iconutil` (system), `convert` (ImageMagick) for ICO.
- Steps:
  1. Render PNG set: 16, 32, 64, 128, 256, 512, 1024 (and @2x variants for macOS).
  2. Build macOS `.iconset` directory, then `iconutil -c icns` → `AppIcon.icns`.
  3. Combine 16/32/48/256 PNGs into `AppIcon.ico`.
  4. Copy the 32×32 PNG to `src/Orbital.App/Assets/tray-icon.png`.
  5. Copy source SVG + 256/512 PNGs to `docs/`.
- Run manually once; commit all output artifacts. Re-run if the SVG changes.

## 6. Release pipeline (Area 3)

### 6.1 Files
```
build/
├── release.sh
├── release-env.sample
├── mac/
│   ├── sign.sh
│   └── entitlements.plist
└── (existing publish-mac.sh, publish-win.ps1, build.sh preserved)
```

### 6.2 `release.sh` flow (fail-fast)

```
1. Preflight:
   - git status clean
   - git branch == main
   - gh auth status = ok
   - dotnet version 10.x
   - load .env (fails if any required var missing)

2. Version:
   - VERSION=$(dotnet msbuild Directory.Build.props -getProperty:Version)
   - Abort if `git ls-remote origin refs/tags/v$VERSION` returns non-empty.

3. Tests:
   - dotnet test -c Release

4. macOS build + sign + notarize (per RID in $MAC_RIDS; default "osx-arm64"):
   - Reuse publish-mac.sh core logic (now parameterized): dotnet publish + bundle assembly.
   - Replace Info.plist placeholders with $VERSION.
   - Call build/mac/sign.sh: codesign (deep, hardened-runtime, with entitlements) every Mach-O inside, then the .app itself.
   - zip for submission.
   - xcrun notarytool submit --wait.
   - xcrun stapler staple.
   - Final zip: Orbital-$RID-$VERSION.zip.

5. Windows cross-compile:
   - dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true.
   - Zip: Orbital-win-x64-$VERSION.zip.

6. Velopack pack (per platform):
   - vpk pack --packId dev.orbital.app --packVersion $VERSION --packDir <publish output dir> --mainExe Orbital.App[.exe] --outputDir publish/velopack/$RID.
   - Result: a self-contained full .nupkg, a delta .nupkg (if previous full exists), a Setup installer (Windows only), and a releases feed manifest (RELEASES or the equivalent Velopack 0.0.x format).

7. GitHub release:
   - Compose release notes: generated from git log between last-tag..HEAD (or a placeholder if first release).
   - gh release create v$VERSION --title "Orbital $VERSION" --notes-file <generated>.
   - gh release upload v$VERSION:
       - Orbital-osx-arm64-$VERSION.zip
       - Orbital-win-x64-$VERSION.zip
       - All vpk outputs for each platform

8. Print release URL + success.
```

### 6.3 `entitlements.plist`
Minimal viable for our app:
```xml
<plist version="1.0"><dict>
  <key>com.apple.security.cs.allow-unsigned-executable-memory</key><true/>
  <key>com.apple.security.cs.disable-library-validation</key><true/>
</dict></plist>
```
(Both needed because .NET's single-file extraction uses runtime code emission. We will confirm the minimal set actually required during implementation; over-permissive hardened-runtime entitlements remain preferable to "crashes at launch".)

### 6.4 Env vars (`release-env.sample`)
```bash
# Apple signing & notarization
export APPLE_ID=you@example.com
export APPLE_TEAM_ID=ABCDE12345
export APPLE_PASSWORD=xxxx-xxxx-xxxx-xxxx  # app-specific password from appleid.apple.com
export CODESIGN_IDENTITY="Developer ID Application: Your Name (ABCDE12345)"

# Optional: override RIDs
# export MAC_RIDS="osx-arm64 osx-x64"

# GH_TOKEN is inferred from `gh auth` — no manual var needed.
```

Real `.env` is gitignored. `release-env.sample` is committed with placeholders only.

### 6.5 Failure recovery
- On notarization failure: script prints the log URL (via `xcrun notarytool log`), preserves the signed-but-unnotarized bundle at `publish/osx-arm64/Orbital.app`, aborts. User diagnoses, fixes (typically entitlements), re-runs.
- On GitHub release step failure: Velopack artifacts remain under `publish/velopack/`. User can manually upload via `gh release upload` or re-run.
- No partial tags: the git tag is only pushed after the release is successfully created and assets uploaded.

## 7. Velopack updater (Area 4)

### 7.1 Package
- `Velopack` NuGet added to `Orbital.App.csproj`.

### 7.2 Program.cs bootstrap
`Velopack.VelopackApp.Build().Run()` is the first line of `Main`, before `SingleInstanceGuard`. This intercepts install/uninstall/restart-after-update subcommands Velopack injects.

```csharp
internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        using var guard = new SingleInstanceGuard();
        if (!guard.TryAcquire()) return;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
}
```

### 7.3 `IUpdateService`
```csharp
public interface IUpdateService
{
    bool IsSupported { get; }   // false in dev-mode (dotnet run) or unpackaged
    bool IsUpdateAvailable { get; }
    string? AvailableVersion { get; }
    Task CheckAsync(CancellationToken ct = default);
    Task ApplyAndRestartAsync();
    event Action? UpdateAvailable;
    event Action<Exception>? UpdateFailed;
}
```

### 7.4 `VelopackUpdateService`
- Constructs `UpdateManager` with `new GithubSource("https://github.com/radaiko/Orbital", null, prerelease: false)`.
- `IsSupported` returns `UpdateManager.IsInstalled`.
- `CheckAsync`: calls `CheckForUpdatesAsync`, caches `UpdateInfo`, fires events, logs via `ILogger`.
- `ApplyAndRestartAsync`: `DownloadUpdatesAsync` → `ApplyUpdatesAndRestart`. The restart is handled by Velopack.
- Runs `CheckAsync` 10 seconds after startup, then every 24 h via `System.Threading.Timer`. Timer disposed with the service.

### 7.5 Tray menu integration
`TrayIconController` gains:
```csharp
public void SetUpdateState(UpdateState state, string? version);
public enum UpdateState { NotSupported, Idle, Available, UpToDate }
```
- `NotSupported`: no menu item (dev mode).
- `Idle`: menu item "Check for updates…" triggers `IUpdateService.CheckAsync`.
- `Available`: item changes to "⬆ Update to v<version>" with bold/colored styling (or just a prefix since NativeMenu styling is limited). Click triggers `ApplyAndRestartAsync`.
- `UpToDate`: label "✓ Up to date" for 3 seconds after a manual check, then reverts to `Idle`.

### 7.6 Error dialogs
On `UpdateFailed`, dispatch a single `IDialogService.ShowErrorAsync("Update failed", "…")`. Log the exception details to file.

## 8. Landing page (Area 5)

### 8.1 GitHub Pages setup
- Repo setting: Pages source = `main` branch, `/docs` folder.
- `.nojekyll` file in `docs/` prevents Jekyll processing.

### 8.2 File layout
```
docs/
├── index.html
├── icon.svg
├── screenshot-overlay.png    # 1440×900 native, shown at 720px wide
├── screenshot-quickadd.png   # 1440×900 native
├── .nojekyll
└── (existing docs/superpowers/** untouched — GH Pages serves whatever is in docs/)
```
Note: `docs/superpowers/**` is irrelevant to the landing page (not linked) but will be published to the Pages site. Acceptable — spec documents are already public in the repo. If sensitive, move superpowers/** up to `.superpowers/` later.

### 8.3 Style tokens (cloning nook visually)
```css
--bg: #0a0a0a;
--surface: #141414;
--border: #262626;
--text: #fafafa;
--text-dim: #888;
--accent: #3b82f6;
--accent-hover: #60a5fa;
--radius: 10px;
--max-w: 960px;
font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
```

### 8.4 Page sections (top to bottom)
1. **Sticky header** — 56px, logo (28×28) + "Orbital" + GitHub link with Octocat SVG; background `rgba(10,10,10,0.85)` + `backdrop-filter: blur(12px)`.
2. **Hero** (`padding: 6rem 0 4rem`, centered):
   - `<h1>Orbital</h1>` clamp(2.5rem, 6vw, 4rem)
   - `<p class="tagline">A keyboard-driven todo list for your desktop.</p>` italic, `var(--text-dim)`
   - `<p class="sub">Two hotkeys. Local JSON. Nothing between you and your list.</p>` max-width 540px
   - Primary CTA `<a class="btn-primary">` → `https://github.com/radaiko/Orbital/releases/latest/download/Orbital-macOS-arm64.zip` (label changes by detected OS; default "Download for macOS")
   - Version badge `<span id="version-badge">` populated via JS
3. **Downloads grid** — three `.dl-card` cells:
   - 🍎 macOS — "Apple Silicon · notarized" → `/releases/latest/download/Orbital-macOS-arm64-<version>.zip`
   - 🪟 Windows — "x64 · portable" → `/releases/latest/download/Orbital-win-x64-<version>.zip`
   - 🐧 Linux — "Build from source" → link to README#build-from-source
   - Detected OS gets `.detected` class (accent border).
4. **Features grid** — six `.feat-card`:
   - ⌨️ **Keyboard-first** — "Every action, every todo, every edit — no mouse required."
   - 🔒 **100% local** — "No account, no cloud, no telemetry. Your data lives in a JSON file."
   - 🔀 **Drag to reorder** — "No priority field. You rank things by dragging."
   - 🌗 **Theme-aware** — "Follows your system light/dark setting."
   - 🔄 **Auto-updates** — "Background checks via GitHub Releases. Update when you choose."
   - 📥 **Quick add anywhere** — "⌃⌥T summons a popup. Type, Enter, gone."
5. **Screenshots** — two centered cards, each showing one of the screenshots with an `img` + caption underneath.
6. **Footer** — GitHub, License (MIT — to be added to repo as `LICENSE`), version. Copyright.

### 8.5 Client-side JS (inline `<script>`, ~40 lines)
- `fetch("https://api.github.com/repos/radaiko/Orbital/releases/latest")` on DOMContentLoaded.
- Populate `#version-badge` with `data.tag_name`.
- Populate primary CTA href by matching user OS → the corresponding asset from `data.assets[].browser_download_url`.
- If no matching asset yet (pre-first-release), CTA falls back to the releases index.
- OS detection: prefer `navigator.userAgentData.platform` when available, fall back to `navigator.userAgent`. Mac: "Darwin" / "Macintosh"; Windows: "Windows"; Linux: everything else.
- The `.dl-card.detected` class is applied on the matching card.

### 8.6 License gate
- MIT LICENSE file added to repo root before first release. `docs/index.html` footer links to it.

## 9. Testing

Unit tests where possible, manual verification where not.

### 9.1 Orbital.Core.Tests — new tests
- `AboutViewModelTests` — version-string formatting, null-version fallback.
- `FileLoggerProviderTests` (if we extract it to Core — likely keep in App): rotation logic preserves exactly 10 files.
- `FirstRunWelcomeTests` (in `AppHostTests` if we add that project; otherwise manual verification).

### 9.2 Manual verification checklist (post-release.sh run on a clean Mac)
1. `release.sh` completes without error. GitHub release created with all expected assets.
2. Download the notarized zip from the release page, unzip, open. **No right-click → Open required** (signed + notarized).
3. Tray icon appears with the real icon (not the blue placeholder).
4. Check the About dialog — version matches release tag.
5. Check log file exists in `~/Library/Application Support/Orbital/logs/`.
6. Delete the app, reinstall from the release — Velopack sees it as freshly installed.
7. Bump version in `Directory.Build.props` to 1.0.1, run `release.sh` again. Go back to installed 1.0.0 app → manual "Check for updates" in tray → "⬆ Update to v1.0.1" appears → click → app updates and relaunches on 1.0.1.
8. Visit `https://radaiko.github.io/Orbital/` — page loads with correct version badge and working download links that serve the 1.0.1 zips.
9. Windows: download Windows zip on a test VM, run the portable exe, confirm hotkeys + tray work. Velopack update flow works there too.

### 9.3 Landing page visual QA
- Desktop (1280+): hero centered, downloads in 3 columns, features in 2–3 columns.
- Tablet (600–1200): features reflow to 2 columns.
- Mobile (<600): everything single column; download cards 2-per-row.
- Both light and dark `prefers-color-scheme` tested (page is dark-only for now; document that).

## 10. Open questions / deliberate gaps

- **CI**: not setting up GitHub Actions release pipeline as part of this spec. Local `release.sh` is the deliverable. CI port is a future concern (simpler with the local script working first).
- **Accessibility permission detection** on macOS: we heuristically detect denial via the hotkey hook's fault. A more robust approach is `AXIsProcessTrustedWithOptions` via P/Invoke — flagged as a follow-up if heuristic proves unreliable.
- **Delta updates with Velopack**: Velopack supports delta .nupkg when a previous full is available. Our first release has no prior, so only a full appears. Subsequent releases produce deltas automatically.
- **MIT LICENSE** file: needs human review/approval of exact text before committing; placeholder reference in this spec.
- **Landing page light mode**: deferred; dark-only for now.
