# Orbital 1.0 Release + Updater + Landing Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Take the MVP Orbital app to a polished 1.0 release: real branding, signed + notarized macOS bundle, portable Windows bundle, in-app updater via Velopack + GitHub Releases, dark-themed GitHub Pages landing page, and a one-command local release script.

**Architecture:** Five work areas, implemented in order so each unblocks the next: branding pipeline → app-level polish (logging, error dialogs, About, theme, accessibility UX, welcome todo) → local release.sh → Velopack in-app updater → landing page → first release. Everything stays cross-platform; the release script builds macOS + Windows from macOS.

**Tech Stack:** existing (.NET 10, Avalonia 12, CommunityToolkit.Mvvm, SharpHook, System.Text.Json, xUnit) + new: `Velopack` NuGet + `Microsoft.Extensions.Logging` + `rsvg-convert` + `iconutil` + `imagemagick` (for ICO) + `gh` CLI + `xcrun codesign/notarytool/stapler`.

**Reference spec:** `docs/superpowers/specs/2026-04-23-orbital-release-updater-design.md`. If the plan and spec conflict, the spec wins — update the plan and re-check.

**Version pinning for new deps:**
- **`Velopack` NuGet:** use the latest stable `0.0.x` at implementation time (Velopack uses pre-1.0 versioning but is production-used). Run `dotnet package search Velopack` and pick the highest non-preview version. Record the chosen version in the csproj and in this plan's self-review section.
- **Velopack CLI (`vpk`):** install via `dotnet tool install -g vpk` (matches the NuGet version).
- **ImageMagick:** `brew install imagemagick` required on dev machine for Windows ICO generation.

**Known uncertainties:**
- `VelopackApp.Build().Run()` must be the first line of `Program.Main`. Velopack's behavior around our single-instance guard needs verification; document any adaptation.
- `UpdateManager.IsInstalled` semantics: may be a static property or instance property depending on version. Use whichever the installed version exposes.
- macOS Accessibility denial detection heuristic (SharpHook exception → OS permission denied) is approximate; verify at implementation time and refine the condition if needed.

---

## Phase A — Branding pipeline

Produces every icon artifact from a single SVG. Must complete before the release script can bundle proper icons.

---

### Task A1: Author the source SVG

**Files:**
- Create: `build/branding/icon.svg`

- [ ] **Step 1: Create `build/branding` directory**

Run:
```bash
mkdir -p build/branding
```

- [ ] **Step 2: Write `build/branding/icon.svg`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024">
  <!-- Background: rounded square, near-black -->
  <rect width="1024" height="1024" rx="192" ry="192" fill="#0a0a0a"/>

  <!-- Orbit ring: tilted ellipse, blue stroke -->
  <g transform="translate(512 512) rotate(-30)">
    <ellipse cx="0" cy="0" rx="340" ry="150"
             fill="none" stroke="#3b82f6" stroke-width="32"
             stroke-linecap="round"/>
    <!-- Orbit node: brighter dot on the ring -->
    <circle cx="340" cy="0" r="48" fill="#60a5fa"/>
  </g>
</svg>
```

Design goals: scalable to 16×16 without losing the ring-plus-dot silhouette, matches landing page background color so the macOS rounded-square mask looks seamless.

- [ ] **Step 3: Preview the SVG**

Run:
```bash
rsvg-convert -w 256 build/branding/icon.svg -o /tmp/icon-preview.png && open /tmp/icon-preview.png
```

Visual check: you should see a dark square with a tilted blue ellipse and one brighter dot on the ring. Close the preview window when satisfied.

- [ ] **Step 4: Commit**

```bash
git add build/branding/icon.svg
git commit -m "Add Orbital source icon SVG"
```

---

### Task A2: Icon generation script

**Files:**
- Create: `build/branding/generate-icons.sh`
- Create: `build/branding/AppIcon.icns` (generated)
- Create: `build/branding/AppIcon.ico` (generated)
- Create: `build/branding/icon-{128,256,512,1024}.png` (generated)
- Modify: `src/Orbital.App/Assets/tray-icon.png` (overwritten)
- Create: `docs/icon.svg` (copy of source)

- [ ] **Step 1: Write the script**

```bash
#!/usr/bin/env bash
# build/branding/generate-icons.sh — rebuild all icon artifacts from icon.svg
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC="$SCRIPT_DIR/icon.svg"

need() { command -v "$1" >/dev/null 2>&1 || { echo "Missing: $1"; echo "  $2"; exit 1; }; }
need rsvg-convert "brew install librsvg"
need iconutil     "ships with macOS"
need magick       "brew install imagemagick"

[ -f "$SRC" ] || { echo "Source SVG missing: $SRC"; exit 1; }

# 1. Landing-page copies
mkdir -p "$ROOT/docs"
cp "$SRC" "$ROOT/docs/icon.svg"

# 2. PNG set for landing page, store listings, macOS iconset
SIZES=(16 32 64 128 256 512 1024)
for s in "${SIZES[@]}"; do
    rsvg-convert -w "$s" -h "$s" "$SRC" -o "$SCRIPT_DIR/icon-$s.png"
done

# 3. macOS iconset + icns
ICONSET="$SCRIPT_DIR/AppIcon.iconset"
rm -rf "$ICONSET"
mkdir -p "$ICONSET"
cp "$SCRIPT_DIR/icon-16.png"   "$ICONSET/icon_16x16.png"
cp "$SCRIPT_DIR/icon-32.png"   "$ICONSET/icon_16x16@2x.png"
cp "$SCRIPT_DIR/icon-32.png"   "$ICONSET/icon_32x32.png"
cp "$SCRIPT_DIR/icon-64.png"   "$ICONSET/icon_32x32@2x.png"
cp "$SCRIPT_DIR/icon-128.png"  "$ICONSET/icon_128x128.png"
cp "$SCRIPT_DIR/icon-256.png"  "$ICONSET/icon_128x128@2x.png"
cp "$SCRIPT_DIR/icon-256.png"  "$ICONSET/icon_256x256.png"
cp "$SCRIPT_DIR/icon-512.png"  "$ICONSET/icon_256x256@2x.png"
cp "$SCRIPT_DIR/icon-512.png"  "$ICONSET/icon_512x512.png"
cp "$SCRIPT_DIR/icon-1024.png" "$ICONSET/icon_512x512@2x.png"
iconutil -c icns -o "$SCRIPT_DIR/AppIcon.icns" "$ICONSET"
rm -rf "$ICONSET"

# 4. Windows ICO (16, 32, 48, 256)
magick -background none \
    "$SCRIPT_DIR/icon-16.png" \
    "$SCRIPT_DIR/icon-32.png" \
    "$SCRIPT_DIR/icon-64.png" \
    "$SCRIPT_DIR/icon-256.png" \
    "$SCRIPT_DIR/AppIcon.ico"

# 5. Tray icon (32×32)
mkdir -p "$ROOT/src/Orbital.App/Assets"
cp "$SCRIPT_DIR/icon-32.png" "$ROOT/src/Orbital.App/Assets/tray-icon.png"

# 6. Only keep the sizes we care about long-term in branding/
KEEP=(128 256 512 1024)
for s in 16 32 64; do
    rm -f "$SCRIPT_DIR/icon-$s.png"
done

echo "Generated:"
echo "  $SCRIPT_DIR/AppIcon.icns"
echo "  $SCRIPT_DIR/AppIcon.ico"
for s in "${KEEP[@]}"; do
    echo "  $SCRIPT_DIR/icon-$s.png"
done
echo "  $ROOT/src/Orbital.App/Assets/tray-icon.png"
echo "  $ROOT/docs/icon.svg"
```

- [ ] **Step 2: Make executable and install prerequisite**

Run:
```bash
chmod +x build/branding/generate-icons.sh
brew install imagemagick
```

Expected: `imagemagick` installs successfully (or is already present). `magick` becomes available.

- [ ] **Step 3: Run the script**

Run:
```bash
./build/branding/generate-icons.sh
```

Expected: list of generated files printed. Verify:
```bash
file build/branding/AppIcon.icns build/branding/AppIcon.ico
```

Expected output:
- `AppIcon.icns: Mac OS X icon, ...`
- `AppIcon.ico: MS Windows icon resource - 4 icons, ...`

- [ ] **Step 4: Visual sanity check**

Run:
```bash
open build/branding/icon-256.png
```

Confirm the 256-px render matches expectations (rounded square, orbit ring, dot). Close.

- [ ] **Step 5: Commit script and generated assets**

```bash
git add build/branding/generate-icons.sh \
        build/branding/AppIcon.icns \
        build/branding/AppIcon.ico \
        build/branding/icon-128.png \
        build/branding/icon-256.png \
        build/branding/icon-512.png \
        build/branding/icon-1024.png \
        src/Orbital.App/Assets/tray-icon.png \
        docs/icon.svg
git commit -m "Add icon generation script and generated artifacts"
```

Note the tray icon replaces the blue-square placeholder — on next app launch the real icon appears.

---

### Task A3: Wire Windows ICO into the Windows build

**Files:**
- Modify: `src/Orbital.App/Orbital.App.csproj`

The Windows executable embeds an ICO via the `ApplicationIcon` property. macOS uses the `.icns` via the `.app` bundle's `Info.plist` (we'll wire that in Phase C).

- [ ] **Step 1: Update csproj**

Open `src/Orbital.App/Orbital.App.csproj` and add to the first `<PropertyGroup>`:

```xml
<ApplicationIcon>..\..\build\branding\AppIcon.ico</ApplicationIcon>
```

- [ ] **Step 2: Verify build still clean**

Run: `dotnet build`

Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Orbital.App/Orbital.App.csproj
git commit -m "Embed AppIcon.ico into Orbital.App Windows build"
```

---

## Phase B — App-level polish

---

### Task B1: Version bump + assembly version injection

**Files:**
- Modify: `Directory.Build.props`

- [ ] **Step 1: Add version properties**

Open `Directory.Build.props`. Inside the existing `<PropertyGroup>`, add:

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
<Company>Orbital</Company>
<Product>Orbital</Product>
```

(The `Company` and `Product` lines may already be present from Phase 1 of the MVP plan — don't duplicate them.)

- [ ] **Step 2: Verify version is readable via msbuild**

Run:
```bash
dotnet msbuild Directory.Build.props -getProperty:Version -nologo
```

Expected: prints `1.0.0` on a line by itself. This is the exact command `release.sh` will use.

- [ ] **Step 3: Verify assembly metadata**

Run: `dotnet build && grep -a "1.0.0" src/Orbital.App/bin/Debug/net10.0/Orbital.App.dll`

Expected: matches found inside the compiled DLL (proving version is embedded).

- [ ] **Step 4: Commit**

```bash
git add Directory.Build.props
git commit -m "Set Version to 1.0.0 for first release"
```

---

### Task B2: File logging infrastructure

**Files:**
- Create: `src/Orbital.App/Logging/FileLoggerProvider.cs`
- Create: `src/Orbital.App/Logging/FileLogger.cs`
- Create: `src/Orbital.App/Logging/LoggingSetup.cs`
- Modify: `src/Orbital.App/Orbital.App.csproj`

We use a minimal hand-rolled file logger rather than pulling a large logging dependency. `Microsoft.Extensions.Logging.Abstractions` is BCL-adjacent; provider + logger are ~60 LOC.

- [ ] **Step 1: Add the abstraction package**

Run:
```bash
dotnet add src/Orbital.App/Orbital.App.csproj package Microsoft.Extensions.Logging.Abstractions --version 9.0.0
```

(If `9.0.0` doesn't resolve, use the latest 9.x. Major version must match whatever the rest of Microsoft.Extensions.* resolves to; 9.0 is current at spec time.)

- [ ] **Step 2: Write `FileLogger`**

```csharp
// src/Orbital.App/Logging/FileLogger.cs
namespace Orbital.App.Logging;

using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

internal sealed class FileLogger : ILogger
{
    private readonly string category;
    private readonly FileLoggerProvider provider;

    public FileLogger(string category, FileLoggerProvider provider)
    {
        this.category = category;
        this.provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= provider.MinimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var message = formatter(state, exception);
        var sb = new StringBuilder();
        sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", System.Globalization.CultureInfo.InvariantCulture));
        sb.Append(" [").Append(LevelTag(logLevel)).Append("] ");
        sb.Append(category).Append(": ").Append(message);
        if (exception is not null) sb.Append(Environment.NewLine).Append(exception);
        sb.Append(Environment.NewLine);
        provider.Write(sb.ToString());
    }

    private static string LevelTag(LogLevel level) => level switch
    {
        LogLevel.Trace       => "TRC",
        LogLevel.Debug       => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning     => "WRN",
        LogLevel.Error       => "ERR",
        LogLevel.Critical    => "CRT",
        _                    => "---",
    };
}
```

- [ ] **Step 3: Write `FileLoggerProvider`**

```csharp
// src/Orbital.App/Logging/FileLoggerProvider.cs
namespace Orbital.App.Logging;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Orbital.Core.Persistence;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> loggers = new();
    private readonly StreamWriter writer;
    private readonly object writeLock = new();

    public LogLevel MinimumLevel { get; }

    public FileLoggerProvider(LogLevel minimumLevel)
    {
        MinimumLevel = minimumLevel;
        var dir = Path.Combine(AppPaths.DataDirectory, "logs");
        Directory.CreateDirectory(dir);

        // Rotation: keep only the most recent 10 log files (including the one
        // we're about to create).
        var existing = new DirectoryInfo(dir).GetFiles("*.log")
            .OrderByDescending(f => f.CreationTimeUtc).ToArray();
        foreach (var old in existing.Skip(9))
        {
            try { old.Delete(); } catch { /* best-effort */ }
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var path = Path.Combine(dir, $"{stamp}.log");
        writer = new StreamWriter(path, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName) =>
        loggers.GetOrAdd(categoryName, c => new FileLogger(c, this));

    internal void Write(string line)
    {
        lock (writeLock)
        {
            writer.Write(line);
        }
    }

    public void Dispose()
    {
        lock (writeLock)
        {
            writer.Dispose();
        }
    }
}
```

- [ ] **Step 4: Write `LoggingSetup` helper**

```csharp
// src/Orbital.App/Logging/LoggingSetup.cs
namespace Orbital.App.Logging;

using System;
using Microsoft.Extensions.Logging;

public static class LoggingSetup
{
    public static (ILoggerFactory factory, FileLoggerProvider fileProvider) Create()
    {
        var level = ParseLevel(Environment.GetEnvironmentVariable("ORBITAL_LOG_LEVEL"));
        var fileProvider = new FileLoggerProvider(level);
        var factory = LoggerFactory.Create(b => b.SetMinimumLevel(level).AddProvider(fileProvider));
        return (factory, fileProvider);
    }

    private static LogLevel ParseLevel(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "trace" => LogLevel.Trace,
        "debug" => LogLevel.Debug,
        "info" or "information" => LogLevel.Information,
        "warn" or "warning" => LogLevel.Warning,
        "error" => LogLevel.Error,
        _ => LogLevel.Information,
    };
}
```

- [ ] **Step 5: Build clean**

Run: `dotnet build`

Expected: 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Orbital.App/Logging/ src/Orbital.App/Orbital.App.csproj
git commit -m "Add file-based logging provider and setup helper"
```

---

### Task B3: Wire logging into App and migrate Debug.WriteLine

**Files:**
- Modify: `src/Orbital.App/App.axaml.cs`
- Modify: `src/Orbital.App/Services/SharpHookGlobalHotkeyService.cs`
- Modify: `src/Orbital.App/Services/QuickAddController.cs` (if needed)
- Modify: `src/Orbital.App/Services/OverlayController.cs`
- Modify: `src/Orbital.App/Services/MacAutoStartService.cs`
- Modify: `src/Orbital.App/Services/TrayIconController.cs` (if it has any logs)

Strategy: give `App` a single `ILoggerFactory`, inject `ILogger<T>` into each service's constructor. Migrate one service at a time.

- [ ] **Step 1: Add logger fields and factory creation to `App`**

Modify `src/Orbital.App/App.axaml.cs`. Add at the top of class:

```csharp
using Microsoft.Extensions.Logging;
using Orbital.App.Logging;
```

Add private fields:

```csharp
private ILoggerFactory? loggerFactory;
private FileLoggerProvider? fileProvider;
private ILogger<App>? log;
```

In `InitializeAsync`, immediately after `AppPaths.EnsureDataDirectoryExists();` (which is called in `OnFrameworkInitializationCompleted` — move this into `InitializeAsync` first if convenient), add:

```csharp
(loggerFactory, fileProvider) = LoggingSetup.Create();
log = loggerFactory.CreateLogger<App>();
log.LogInformation("Orbital starting — version {Version}",
    typeof(App).Assembly.GetName().Version?.ToString(3));
```

Replace the existing `Debug.WriteLine(...)` calls in `App.axaml.cs` with:

- `Debug.WriteLine($"Failed to start global hotkey hook: {ex}");` → `log!.LogError(ex, "Failed to start global hotkey hook");`
- Continuation fault: `Debug.WriteLine($"Global hotkey hook failed: {t.Exception}");` → `log!.LogError(t.Exception, "Global hotkey hook failed asynchronously");`
- In `OpenDataFolder`: `log?.LogError(ex, "Failed to open data folder");`
- In `ShowSettings` catch: `log!.LogError(ex, "Failed to save settings");`
- In `Dispose` hotkey catch: `log?.LogError(ex, "Hotkey disposal failed");`

In the tray `QuitRequested` handler, after `desktop.Shutdown();`, NULL the logger? No — instead, dispose loggers in `Dispose()`:

Modify `App.Dispose()`:
```csharp
public void Dispose()
{
    tray?.Dispose();
    if (hotkeys is not null)
    {
        try { hotkeys.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        catch (Exception ex) { log?.LogError(ex, "Hotkey disposal failed"); }
    }
    host?.Dispose();
    loggerFactory?.Dispose();
    fileProvider?.Dispose();
    GC.SuppressFinalize(this);
}
```

- [ ] **Step 2: Migrate `SharpHookGlobalHotkeyService`**

Modify `src/Orbital.App/Services/SharpHookGlobalHotkeyService.cs`:

Add constructor:
```csharp
private readonly ILogger<SharpHookGlobalHotkeyService> log;

public SharpHookGlobalHotkeyService(ILogger<SharpHookGlobalHotkeyService> log)
{
    this.log = log;
}
```

Remove the parameterless constructor (callers must provide a logger).

Update `App.InitializeAsync`:
```csharp
hotkeys = new SharpHookGlobalHotkeyService(loggerFactory!.CreateLogger<SharpHookGlobalHotkeyService>());
```

This service has no other logs today; we pass the logger so the Accessibility-detection task (B9) has somewhere to log.

- [ ] **Step 3: Migrate `MacAutoStartService`**

Modify `src/Orbital.App/Services/MacAutoStartService.cs`:

```csharp
private readonly ILogger<MacAutoStartService>? log;

public MacAutoStartService(ILogger<MacAutoStartService>? log = null)
{
    this.log = log;
}

// In Run():
catch (Exception ex)
{
    log?.LogWarning(ex, "launchctl invocation failed: {Args}", args);
}
```

The `log` is nullable + optional so the factory's parameterless constructor still works. Update `AutoStartServiceFactory`:

```csharp
public static IAutoStartService Create(ILoggerFactory? loggers = null)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsAutoStartService();
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return new MacAutoStartService(loggers?.CreateLogger<MacAutoStartService>());
    return new LinuxAutoStartService();
}
```

In `App`, replace the field initializer:
```csharp
private IAutoStartService? autoStart;
```

And in `InitializeAsync` after the logger is set up:
```csharp
autoStart = AutoStartServiceFactory.Create(loggerFactory);
```

Update any other uses of `autoStart` to null-check.

- [ ] **Step 4: Build + test**

Run: `dotnet build && dotnet test`

Expected: 0 warnings, 0 errors, 79 tests pass.

- [ ] **Step 5: Manual smoke verify**

Run: `dotnet run --project src/Orbital.App` then immediately Ctrl+C after a second. Verify:
```bash
ls -la ~/Library/Application\ Support/Orbital/logs/
```

Expected: at least one `yyyyMMdd_HHmmss.log` file with the startup info line.

- [ ] **Step 6: Commit**

```bash
git add src/Orbital.App/App.axaml.cs \
        src/Orbital.App/Services/SharpHookGlobalHotkeyService.cs \
        src/Orbital.App/Services/MacAutoStartService.cs \
        src/Orbital.App/Services/AutoStartServiceFactory.cs
git commit -m "Migrate App, hotkey service, autostart to ILogger"
```

---

### Task B4: `IDialogService` + message window

**Files:**
- Create: `src/Orbital.App/Services/IDialogService.cs`
- Create: `src/Orbital.App/Services/DialogService.cs`
- Create: `src/Orbital.App/Views/MessageWindow.axaml`
- Create: `src/Orbital.App/Views/MessageWindow.axaml.cs`

- [ ] **Step 1: Interface**

```csharp
// src/Orbital.App/Services/IDialogService.cs
namespace Orbital.App.Services;

using System.Threading.Tasks;

public interface IDialogService
{
    Task ShowErrorAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "OK", string cancelLabel = "Cancel");
}
```

- [ ] **Step 2: Message window XAML**

```xml
<!-- src/Orbital.App/Views/MessageWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="Orbital.App.Views.MessageWindow"
        Title="Orbital"
        Width="440" Height="200"
        CanResize="False"
        WindowStartupLocation="CenterScreen">
    <StackPanel Margin="24" Spacing="12">
        <TextBlock Name="TitleText" FontSize="16" FontWeight="SemiBold" />
        <TextBlock Name="MessageText" TextWrapping="Wrap" />
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8">
            <Button Name="CancelButton" Content="Cancel" IsVisible="False" />
            <Button Name="ConfirmButton" Content="OK" IsDefault="True" />
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 3: Code-behind**

```csharp
// src/Orbital.App/Views/MessageWindow.axaml.cs
namespace Orbital.App.Views;

using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public sealed partial class MessageWindow : Window
{
    private TaskCompletionSource<bool> tcs = new();

    public MessageWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("ConfirmButton")!.Click += (_, _) => Close(true);
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close(false);
        Closed += (_, _) => tcs.TrySetResult(false);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public Task<bool> ShowAsync(Window? owner, string title, string message, string confirmLabel, string? cancelLabel)
    {
        Title = title;
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
        var confirm = this.FindControl<Button>("ConfirmButton")!;
        confirm.Content = confirmLabel;
        var cancel = this.FindControl<Button>("CancelButton")!;
        cancel.IsVisible = cancelLabel is not null;
        if (cancelLabel is not null) cancel.Content = cancelLabel;

        tcs = new TaskCompletionSource<bool>();
        if (owner is not null) ShowDialog(owner);
        else Show();
        return tcs.Task;
    }

    private void Close(bool result)
    {
        tcs.TrySetResult(result);
        Close();
    }
}
```

- [ ] **Step 4: `DialogService` implementation**

```csharp
// src/Orbital.App/Services/DialogService.cs
namespace Orbital.App.Services;

using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Orbital.App.Views;

public sealed class DialogService : IDialogService
{
    public Task ShowErrorAsync(string title, string message) =>
        RunAsync(title, message, "OK", cancelLabel: null).ContinueWith(_ => { });

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "OK", string cancelLabel = "Cancel") =>
        RunAsync(title, message, confirmLabel, cancelLabel);

    private static Task<bool> RunAsync(string title, string message, string confirmLabel, string? cancelLabel)
    {
        return Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = (Avalonia.Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var w = new MessageWindow();
            return await w.ShowAsync(owner, title, message, confirmLabel, cancelLabel);
        }).GetTask().Unwrap();
    }
}
```

Note: if `GetTask().Unwrap()` doesn't compile (Avalonia's `InvokeAsync` signature), replace with:
```csharp
return await Dispatcher.UIThread.InvokeAsync(() => …);
```
and make the outer method `async Task<bool>`. Follow what the IDE suggests.

- [ ] **Step 5: Wire into `App`**

Add field `private IDialogService? dialogs;` and initialize in `InitializeAsync` after the logger:
```csharp
dialogs = new DialogService();
```

- [ ] **Step 6: Build clean**

Run: `dotnet build`

Expected: 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add src/Orbital.App/Services/IDialogService.cs \
        src/Orbital.App/Services/DialogService.cs \
        src/Orbital.App/Views/MessageWindow.axaml \
        src/Orbital.App/Views/MessageWindow.axaml.cs \
        src/Orbital.App/App.axaml.cs
git commit -m "Add IDialogService + MessageWindow for user-visible errors"
```

---

### Task B5: Use the dialog service for save-failure paths

**Files:**
- Modify: `src/Orbital.App/App.axaml.cs`

- [ ] **Step 1: Replace the `Debug.WriteLine` in `ShowSettings` with a user-visible dialog**

In `ShowSettings` inside `SaveRequested += async`:

```csharp
w.SaveRequested += async () =>
{
    try
    {
        host.Settings = vm.Build();
        await host.SaveSettingsAsync();
    }
    catch (Exception ex)
    {
        log!.LogError(ex, "Failed to save settings");
        await dialogs!.ShowErrorAsync(
            "Couldn't save settings",
            "Something went wrong writing settings.json. Your changes were not saved. See the log file for details.");
    }
    finally
    {
        w.Close();
    }
};
```

- [ ] **Step 2: Build clean; commit**

Run: `dotnet build`

```bash
git add src/Orbital.App/App.axaml.cs
git commit -m "Show user-visible error dialog on settings save failure"
```

---

### Task B6: Theme-aware overlay/quickadd colors

**Files:**
- Modify: `src/Orbital.App/App.axaml`
- Modify: `src/Orbital.App/Views/OverlayWindow.axaml`
- Modify: `src/Orbital.App/Views/QuickAddWindow.axaml`

- [ ] **Step 1: Declare theme-aware resources in `App.axaml`**

Replace the `Application.Styles` block (or add a sibling `Application.Resources` block) with:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Orbital.App.App"
             RequestedThemeVariant="Default">
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.ThemeDictionaries>
                <ResourceDictionary x:Key="Dark">
                    <SolidColorBrush x:Key="OrbitalOverlayBackgroundBrush" Color="#CC1E1E1E"/>
                    <SolidColorBrush x:Key="OrbitalOverlayBorderBrush" Color="#33FFFFFF"/>
                    <SolidColorBrush x:Key="OrbitalOverlayForegroundBrush" Color="#FFFFFFFF"/>
                    <SolidColorBrush x:Key="OrbitalOverlayDimBrush" Color="#80FFFFFF"/>
                    <SolidColorBrush x:Key="OrbitalOverlayChipBackgroundBrush" Color="#22FFFFFF"/>
                </ResourceDictionary>
                <ResourceDictionary x:Key="Light">
                    <SolidColorBrush x:Key="OrbitalOverlayBackgroundBrush" Color="#F2FAFAFA"/>
                    <SolidColorBrush x:Key="OrbitalOverlayBorderBrush" Color="#33000000"/>
                    <SolidColorBrush x:Key="OrbitalOverlayForegroundBrush" Color="#FF1E1E1E"/>
                    <SolidColorBrush x:Key="OrbitalOverlayDimBrush" Color="#801E1E1E"/>
                    <SolidColorBrush x:Key="OrbitalOverlayChipBackgroundBrush" Color="#221E1E1E"/>
                </ResourceDictionary>
            </ResourceDictionary.ThemeDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: Update `OverlayWindow.axaml` to use the resources**

Replace the hard-coded `Background="#CC1E1E1E"` with `Background="{DynamicResource OrbitalOverlayBackgroundBrush}"`.

Replace `BorderBrush="#33FFFFFF"` with `BorderBrush="{DynamicResource OrbitalOverlayBorderBrush}"`.

Replace every `Foreground="White"` with `Foreground="{DynamicResource OrbitalOverlayForegroundBrush}"`.

Replace `Foreground="#B0FFFFFF"` and similar dim foregrounds with `Foreground="{DynamicResource OrbitalOverlayDimBrush}"`.

Replace `Background="#22FFFFFF"` (chip background) with `Background="{DynamicResource OrbitalOverlayChipBackgroundBrush}"`.

- [ ] **Step 3: Same migration for `QuickAddWindow.axaml`**

Same pattern — swap hard-coded colors to DynamicResource.

- [ ] **Step 4: Build and run**

Run: `dotnet build && dotnet run --project src/Orbital.App` briefly.

Expected: both windows render with no XAML-binding errors in console output. Light/dark switch follows macOS's Appearance setting.

- [ ] **Step 5: Commit**

```bash
git add src/Orbital.App/App.axaml src/Orbital.App/Views/OverlayWindow.axaml src/Orbital.App/Views/QuickAddWindow.axaml
git commit -m "Replace hard-coded overlay/quickadd colors with theme-aware resources"
```

---

### Task B7: First-run welcome todo

**Files:**
- Modify: `src/Orbital.Core/Persistence/ISettingsStore.cs`
- Modify: `src/Orbital.Core/Persistence/JsonSettingsStore.cs`
- Modify: `src/Orbital.App/AppHost.cs`
- Modify: `tests/Orbital.Core.Tests/Persistence/JsonSettingsStoreTests.cs`

- [ ] **Step 1: Extend interface**

```csharp
// src/Orbital.Core/Persistence/ISettingsStore.cs
namespace Orbital.Core.Persistence;

using Orbital.Core.Models;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    // Returns the settings plus a flag indicating whether the file existed.
    // If false, the returned settings are the defaults and no file has ever been written.
    Task<(AppSettings settings, bool existed)> LoadWithProvenanceAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
```

- [ ] **Step 2: Update `JsonSettingsStore`**

Replace the old `LoadAsync` body with a wrapper that calls `LoadWithProvenanceAsync`:

```csharp
public async Task<AppSettings> LoadAsync(CancellationToken ct = default) =>
    (await LoadWithProvenanceAsync(ct)).settings;

public async Task<(AppSettings settings, bool existed)> LoadWithProvenanceAsync(CancellationToken ct = default)
{
    if (!File.Exists(filePath)) return (new AppSettings(), false);
    var json = await File.ReadAllTextAsync(filePath, ct);
    if (string.IsNullOrWhiteSpace(json)) return (new AppSettings(), true);
    var s = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
    return (s, true);
}
```

- [ ] **Step 3: Write a failing test**

Add to `tests/Orbital.Core.Tests/Persistence/JsonSettingsStoreTests.cs`:

```csharp
[Fact]
public async Task LoadWithProvenanceAsync_reports_false_when_file_missing()
{
    var (_, existed) = await store.LoadWithProvenanceAsync();
    existed.Should().BeFalse();
}

[Fact]
public async Task LoadWithProvenanceAsync_reports_true_after_save()
{
    await store.SaveAsync(new AppSettings());
    var (_, existed) = await store.LoadWithProvenanceAsync();
    existed.Should().BeTrue();
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test`

Expected: all pass (test count now 81).

- [ ] **Step 5: Use provenance in `AppHost.LoadAsync`**

```csharp
public async Task LoadAsync()
{
    var (settings, settingsExisted) = await settingsStore.LoadWithProvenanceAsync();
    Settings = settings;
    var todos = await todoStore.LoadAsync();
    Todos.Clear();
    foreach (var t in todos) Todos.Add(t);

    if (!settingsExisted && Todos.Count == 0)
    {
        Todos.Add(new Todo
        {
            Id = Guid.NewGuid(),
            Title = "Welcome to Orbital — press Ctrl+Alt+L to open, Ctrl+Alt+T to add",
            CreatedAt = DateTimeOffset.Now,
            Order = 0,
        });
        ScheduleSaveTodos();
    }
}
```

- [ ] **Step 6: Build and test**

Run: `dotnet build && dotnet test`

Expected: 81 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Orbital.Core/Persistence/ISettingsStore.cs \
        src/Orbital.Core/Persistence/JsonSettingsStore.cs \
        src/Orbital.App/AppHost.cs \
        tests/Orbital.Core.Tests/Persistence/JsonSettingsStoreTests.cs
git commit -m "Add first-run welcome todo via settings provenance flag"
```

---

### Task B8: About dialog

**Files:**
- Create: `src/Orbital.Core/ViewModels/AboutViewModel.cs`
- Create: `src/Orbital.App/Views/AboutWindow.axaml`
- Create: `src/Orbital.App/Views/AboutWindow.axaml.cs`
- Modify: `src/Orbital.App/Services/TrayIconController.cs`
- Modify: `src/Orbital.App/App.axaml.cs`

- [ ] **Step 1: View model**

```csharp
// src/Orbital.Core/ViewModels/AboutViewModel.cs
namespace Orbital.Core.ViewModels;

public sealed class AboutViewModel
{
    public string Version { get; }
    public string RepositoryUrl => "https://github.com/radaiko/Orbital";
    public string Copyright => $"© {DateTime.Now.Year} radaiko";

    public AboutViewModel(string version) => Version = version;
}
```

- [ ] **Step 2: About window XAML**

```xml
<!-- src/Orbital.App/Views/AboutWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Orbital.Core.ViewModels"
        x:Class="Orbital.App.Views.AboutWindow"
        x:DataType="vm:AboutViewModel"
        Title="About Orbital"
        Width="360" Height="320"
        CanResize="False"
        WindowStartupLocation="CenterScreen">
    <StackPanel Margin="24" Spacing="8" HorizontalAlignment="Center">
        <Image Source="avares://Orbital.App/Assets/tray-icon.png" Width="64" Height="64" />
        <TextBlock Text="Orbital" FontSize="22" FontWeight="SemiBold" HorizontalAlignment="Center" />
        <TextBlock Text="{Binding Version, StringFormat='v{0}'}" HorizontalAlignment="Center" Opacity="0.7" />
        <Button Name="CheckUpdatesButton" Content="Check for updates" HorizontalAlignment="Center" Margin="0,16,0,0" />
        <TextBlock Name="UpdateStatus" HorizontalAlignment="Center" Opacity="0.7" FontSize="11" />
        <TextBlock Margin="0,16,0,0" HorizontalAlignment="Center">
            <Hyperlink Name="RepoLink" NavigateUri="https://github.com/radaiko/Orbital">GitHub</Hyperlink>
        </TextBlock>
        <TextBlock Text="{Binding Copyright}" HorizontalAlignment="Center" Opacity="0.7" FontSize="11" />
    </StackPanel>
</Window>
```

**Note:** Avalonia 12 may not ship a `<Hyperlink>` inline element inside `TextBlock` out of the box (WPF has it; Avalonia uses a custom `HyperlinkButton`). If the above fails to compile, replace the `<TextBlock>` containing it with:

```xml
<Button Name="RepoLink" Content="GitHub" Classes="hyperlink" Background="Transparent" />
```

And handle the click in code-behind to open the URL. Document in your report which path you took.

- [ ] **Step 3: Code-behind**

```csharp
// src/Orbital.App/Views/AboutWindow.axaml.cs
namespace Orbital.App.Views;

using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Orbital.Core.ViewModels;

public sealed partial class AboutWindow : Window
{
    public event Func<bool>? CheckUpdatesRequested; // bool = update-available

    public AboutWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("CheckUpdatesButton")!.Click += OnCheckUpdates;
        // If using the Button-style repo link:
        var repoLink = this.FindControl<Button>("RepoLink");
        if (repoLink is not null) repoLink.Click += (_, _) => OpenUrl("https://github.com/radaiko/Orbital");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCheckUpdates(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("UpdateStatus")!;
        status.Text = "Checking…";
        try
        {
            var found = CheckUpdatesRequested?.Invoke() ?? false;
            status.Text = found ? "Update available — see tray menu." : "You're up to date.";
        }
        catch (Exception ex)
        {
            status.Text = $"Check failed: {ex.Message}";
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }
}
```

- [ ] **Step 4: Add tray menu item "About Orbital…"**

Modify `TrayIconController.BuildMenu` to add above Quit:

```csharp
var about = new NativeMenuItem("About Orbital…");
about.Click += (_, _) => AboutRequested?.Invoke();
menu.Add(about);
```

And add the event at the top of the class:

```csharp
public event Action? AboutRequested;
```

- [ ] **Step 5: Wire from `App`**

Add private method:

```csharp
private void ShowAbout()
{
    var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "dev";
    var vm = new AboutViewModel(version);
    var w = new AboutWindow { DataContext = vm };
    // Update-check hookup is Task D2+ — for now, a stub:
    w.CheckUpdatesRequested = () => false;
    w.Show();
}
```

Wire from tray:
```csharp
tray!.AboutRequested += ShowAbout;
```

- [ ] **Step 6: Build + run briefly**

Run: `dotnet build && dotnet run --project src/Orbital.App` for a few seconds, then open tray → About. Confirm window appears with "v1.0.0".

- [ ] **Step 7: Commit**

```bash
git add src/Orbital.Core/ViewModels/AboutViewModel.cs \
        src/Orbital.App/Views/AboutWindow.axaml \
        src/Orbital.App/Views/AboutWindow.axaml.cs \
        src/Orbital.App/Services/TrayIconController.cs \
        src/Orbital.App/App.axaml.cs
git commit -m "Add About dialog with version display and update-check stub"
```

---

### Task B9: macOS Accessibility permission banner

**Files:**
- Modify: `src/Orbital.App/Services/TrayIconController.cs`
- Modify: `src/Orbital.App/App.axaml.cs`

- [ ] **Step 1: State + event on `TrayIconController`**

Add field:
```csharp
private bool accessibilityDenied;
private NativeMenuItem? accessibilityMenuItem;
```

Add method:
```csharp
public void SetAccessibilityDenied(bool denied)
{
    if (accessibilityDenied == denied || trayIcon is null) return;
    accessibilityDenied = denied;
    RebuildMenuWithAccessibility();
}

private void RebuildMenuWithAccessibility()
{
    if (trayIcon is null) return;
    var menu = BuildMenu();
    if (accessibilityDenied)
    {
        var item = new NativeMenuItem("⚠ Grant Accessibility…");
        item.Click += (_, _) => OpenAccessibilityPane();
        menu.Items.Insert(0, item);
        menu.Items.Insert(1, new NativeMenuItemSeparator());
    }
    trayIcon.Menu = menu;
}

private static void OpenAccessibilityPane()
{
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "open",
            "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")
        {
            UseShellExecute = false,
        });
    }
    catch { /* best-effort */ }
}
```

- [ ] **Step 2: Detect denial from `App`**

In the `ContinueWith` fault handler (Task B3 migrated this to `log.LogError`), also:

```csharp
_ = startTask.ContinueWith(t =>
{
    log!.LogError(t.Exception, "Global hotkey hook failed asynchronously");
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        Dispatcher.UIThread.Post(() => tray?.SetAccessibilityDenied(true));
        Dispatcher.UIThread.Post(() => dialogs!.ShowErrorAsync(
            "Accessibility permission required",
            "Orbital needs Accessibility access to listen for your hotkeys. Open System Settings → Privacy & Security → Accessibility, toggle Orbital on, then quit and relaunch the app."));
    }
}, TaskContinuationOptions.OnlyOnFaulted);
```

Add `using System.Runtime.InteropServices;` and `using Avalonia.Threading;` if not already.

- [ ] **Step 3: Build clean**

Run: `dotnet build`

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Services/TrayIconController.cs src/Orbital.App/App.axaml.cs
git commit -m "Show Accessibility banner in tray and dialog on macOS denial"
```

---

## Phase C — Release pipeline

---

### Task C1: Entitlements file and sign helper

**Files:**
- Create: `build/mac/entitlements.plist`
- Create: `build/mac/sign.sh`
- Create: `build/release-env.sample`

- [ ] **Step 1: Entitlements**

```xml
<!-- build/mac/entitlements.plist -->
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.cs.disable-library-validation</key>
    <true/>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
</dict>
</plist>
```

- [ ] **Step 2: Sign helper**

```bash
#!/usr/bin/env bash
# build/mac/sign.sh — codesign + notarize a .app bundle
# Args: $1 = path to .app bundle, $2 = output zip path
set -euo pipefail

APP="$1"
ZIP="$2"
[ -d "$APP" ] || { echo "App bundle not found: $APP"; exit 1; }

: "${CODESIGN_IDENTITY:?Set CODESIGN_IDENTITY in .env (e.g. 'Developer ID Application: Your Name (XXXXXXXXXX)')}"
: "${APPLE_ID:?Set APPLE_ID in .env}"
: "${APPLE_TEAM_ID:?Set APPLE_TEAM_ID in .env}"
: "${APPLE_PASSWORD:?Set APPLE_PASSWORD in .env (app-specific password)}"

ENTITLEMENTS="$(cd "$(dirname "$0")" && pwd)/entitlements.plist"

echo "▸ Signing $APP"
# Deep sign every Mach-O inside. --force overwrites any ad-hoc sigs from dotnet publish.
codesign --deep --force --options runtime --timestamp \
    --entitlements "$ENTITLEMENTS" \
    --sign "$CODESIGN_IDENTITY" \
    "$APP"

echo "▸ Verifying signature"
codesign --verify --deep --strict --verbose=2 "$APP"

echo "▸ Zipping for notarization"
ditto -c -k --keepParent "$APP" "$ZIP"

echo "▸ Submitting to Apple notary"
xcrun notarytool submit "$ZIP" \
    --apple-id "$APPLE_ID" \
    --team-id "$APPLE_TEAM_ID" \
    --password "$APPLE_PASSWORD" \
    --wait

echo "▸ Stapling ticket"
xcrun stapler staple "$APP"

echo "▸ Re-zipping stapled bundle"
rm -f "$ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"

echo "✓ Signed + notarized: $ZIP"
```

- [ ] **Step 3: Env var template**

```bash
# build/release-env.sample
# Copy to .env (gitignored) and fill in.
#
# Apple signing & notarization (required for release.sh):
export APPLE_ID=you@example.com
export APPLE_TEAM_ID=ABCDE12345
export APPLE_PASSWORD=xxxx-xxxx-xxxx-xxxx
export CODESIGN_IDENTITY="Developer ID Application: Your Name (ABCDE12345)"

# Optional: override target RIDs
# export MAC_RIDS="osx-arm64 osx-x64"

# GH_TOKEN is inferred from `gh auth status` — no manual var needed.
```

- [ ] **Step 4: Add `.env` to gitignore**

Open `.gitignore` and append:

```
# Local environment / secrets
.env
.env.local
```

- [ ] **Step 5: Make sign.sh executable; commit**

```bash
chmod +x build/mac/sign.sh
git add build/mac/ build/release-env.sample .gitignore
git commit -m "Add macOS signing + notarization helper and env template"
```

---

### Task C2: `release.sh` — preflight, version, tests, macOS

**Files:**
- Create: `build/release.sh`

- [ ] **Step 1: Write the script (Part 1 — preflight through macOS)**

```bash
#!/usr/bin/env bash
# build/release.sh — one-command local release pipeline
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH="$ROOT/publish"
MAC_RIDS="${MAC_RIDS:-osx-arm64}"

# --- Preflight ---
echo "▸ Preflight"

# Clean working tree
if ! git -C "$ROOT" diff-index --quiet HEAD --; then
    echo "✗ Working tree has uncommitted changes. Commit or stash first."
    exit 1
fi

# Must be on main
BRANCH="$(git -C "$ROOT" rev-parse --abbrev-ref HEAD)"
[ "$BRANCH" = "main" ] || { echo "✗ Not on main (currently '$BRANCH')"; exit 1; }

# Load .env
ENV_FILE="$ROOT/.env"
[ -f "$ENV_FILE" ] || { echo "✗ $ENV_FILE missing. Copy build/release-env.sample → .env and fill in."; exit 1; }
# shellcheck source=/dev/null
source "$ENV_FILE"

# Check required tools
command -v dotnet    >/dev/null || { echo "✗ dotnet not in PATH"; exit 1; }
command -v gh        >/dev/null || { echo "✗ gh (GitHub CLI) not in PATH"; exit 1; }
command -v xcrun     >/dev/null || { echo "✗ xcrun not in PATH (need Xcode CLI tools)"; exit 1; }
command -v vpk       >/dev/null || { echo "✗ vpk not in PATH. Run: dotnet tool install -g vpk"; exit 1; }
gh auth status >/dev/null 2>&1 || { echo "✗ gh not authenticated. Run 'gh auth login'."; exit 1; }

# --- Version ---
VERSION="$(dotnet msbuild "$ROOT/Directory.Build.props" -getProperty:Version -nologo | tr -d '[:space:]')"
[ -n "$VERSION" ] || { echo "✗ Could not read Version from Directory.Build.props"; exit 1; }
echo "▸ Version: $VERSION"

TAG="v$VERSION"
if git -C "$ROOT" ls-remote origin "refs/tags/$TAG" | grep -q "$TAG"; then
    echo "✗ Tag $TAG already exists on origin. Bump Directory.Build.props Version."
    exit 1
fi

# --- Tests ---
echo "▸ Running tests"
dotnet test "$ROOT/Orbital.sln" -c Release --nologo --verbosity minimal

# --- macOS build + sign + notarize ---
mkdir -p "$PUBLISH"
MAC_ARTIFACTS=()

for RID in $MAC_RIDS; do
    echo "▸ Building macOS bundle for $RID"
    OUT="$PUBLISH/$RID"
    APP="$OUT/Orbital.app"
    rm -rf "$APP"

    dotnet publish "$ROOT/src/Orbital.App/Orbital.App.csproj" \
        -c Release -r "$RID" --self-contained true \
        -p:PublishSingleFile=false \
        -o "$OUT/payload" --nologo --verbosity minimal

    mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
    cp -R "$OUT/payload/"* "$APP/Contents/MacOS/"

    # Info.plist with version substituted
    sed "s/0\.1\.0/$VERSION/g" "$ROOT/build/macos/Info.plist.template" > "$APP/Contents/Info.plist"

    # App icon
    cp "$ROOT/build/branding/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
    # Reference it in Info.plist
    /usr/libexec/PlistBuddy -c "Add :CFBundleIconFile string AppIcon" "$APP/Contents/Info.plist" 2>/dev/null || \
    /usr/libexec/PlistBuddy -c "Set  :CFBundleIconFile AppIcon" "$APP/Contents/Info.plist"

    chmod +x "$APP/Contents/MacOS/Orbital.App"

    # Sign + notarize
    ZIP="$OUT/Orbital-$RID-$VERSION.zip"
    "$ROOT/build/mac/sign.sh" "$APP" "$ZIP"

    MAC_ARTIFACTS+=("$ZIP")
done

echo "▸ macOS artifacts:"
printf '  %s\n' "${MAC_ARTIFACTS[@]}"

# --- Windows cross-compile (next task) ---
# ... continues in Task C3

# --- Velopack pack (Task C4) ---
# ...

# --- GitHub release (Task C5) ---
# ...
```

Note the PlistBuddy block sets `CFBundleIconFile` to `AppIcon` (which resolves to `Contents/Resources/AppIcon.icns`). Double-check that the Info.plist template from Phase 9 still has version `0.1.0` so the `sed` substitution above matches; adjust the sed pattern if the template has a different placeholder.

- [ ] **Step 2: Make script executable**

Run: `chmod +x build/release.sh`

- [ ] **Step 3: Smoke-test preflight (should fail with no .env)**

Run: `./build/release.sh 2>&1 | head -5`

Expected: `✗ /Users/.../Orbital/.env missing.` — proves preflight runs.

- [ ] **Step 4: Commit the partial script**

```bash
git add build/release.sh
git commit -m "Add release.sh with preflight, version, tests, and macOS sign pipeline"
```

The script is incomplete (no Windows, no Velopack, no GitHub release yet). The rest is added in C3–C5.

---

### Task C3: `release.sh` — Windows cross-compile

**Files:**
- Modify: `build/release.sh`

- [ ] **Step 1: Append the Windows section to `release.sh`**

After the `MAC_ARTIFACTS` block, before the trailing comments, insert:

```bash
# --- Windows cross-compile ---
echo "▸ Building Windows x64"
WIN_OUT="$PUBLISH/win-x64"
rm -rf "$WIN_OUT"

dotnet publish "$ROOT/src/Orbital.App/Orbital.App.csproj" \
    -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$WIN_OUT/payload" --nologo --verbosity minimal

WIN_ZIP="$WIN_OUT/Orbital-win-x64-$VERSION.zip"
rm -f "$WIN_ZIP"
(cd "$WIN_OUT" && zip -r "$(basename "$WIN_ZIP")" payload > /dev/null)

echo "▸ Windows artifact: $WIN_ZIP"
```

- [ ] **Step 2: Smoke test (skip preflight by setting env temporarily)**

Rather than run the full script, verify just the Windows publish command works:

```bash
dotnet publish src/Orbital.App/Orbital.App.csproj \
    -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o /tmp/orbital-winx64-test --nologo --verbosity minimal
ls /tmp/orbital-winx64-test/Orbital.App.exe
```

Expected: `Orbital.App.exe` exists. Clean up: `rm -rf /tmp/orbital-winx64-test`.

- [ ] **Step 3: Commit**

```bash
git add build/release.sh
git commit -m "release.sh: add Windows cross-compile step"
```

---

### Task C4: `release.sh` — Velopack pack

**Files:**
- Modify: `build/release.sh`
- Modify: `src/Orbital.App/Orbital.App.csproj` (add Velopack NuGet for D1 dependency ordering)

- [ ] **Step 1: Install the Velopack CLI once**

Run:
```bash
dotnet tool install -g vpk
vpk --help | head -5
```

Expected: help output. If the tool was previously installed, update with `dotnet tool update -g vpk`.

- [ ] **Step 2: Add Velopack NuGet to App (for D1)**

Run:
```bash
dotnet add src/Orbital.App/Orbital.App.csproj package Velopack
```

This picks the current latest stable. Record the resolved version in your report. Verify `dotnet build` still passes.

- [ ] **Step 3: Append Velopack section to `release.sh`**

Insert after the Windows section:

```bash
# --- Velopack pack ---
echo "▸ Packing Velopack update artifacts"
VPK_OUT="$PUBLISH/velopack"
rm -rf "$VPK_OUT"
mkdir -p "$VPK_OUT"

# macOS (one entry per RID)
for RID in $MAC_RIDS; do
    vpk_platform="osx"
    echo "  ▸ vpk pack $RID"
    vpk pack \
        --packId dev.orbital.app \
        --packVersion "$VERSION" \
        --packDir "$PUBLISH/$RID/payload" \
        --mainExe Orbital.App \
        --outputDir "$VPK_OUT/$RID" \
        --runtime "$vpk_platform-$(echo "$RID" | sed 's/osx-//')"
done

# Windows
echo "  ▸ vpk pack win-x64"
vpk pack \
    --packId dev.orbital.app \
    --packVersion "$VERSION" \
    --packDir "$PUBLISH/win-x64/payload" \
    --mainExe Orbital.App.exe \
    --outputDir "$VPK_OUT/win-x64" \
    --runtime win-x64

echo "▸ Velopack packages:"
find "$VPK_OUT" -type f \( -name '*.nupkg' -o -name '*Setup*' -o -name 'RELEASES*' -o -name '*.zip' \)
```

Notes on the vpk command:
- The `--runtime` flag format may vary by Velopack version; at implementation time run `vpk pack --help` and consult the installed version's syntax.
- If `--mainExe Orbital.App` needs the `.exe` suffix on macOS, vpk typically handles cross-platform correctly; adjust per `vpk pack --help` output.
- Output typically contains a `.nupkg` per platform + a `RELEASES` feed file. These are uploaded to the GitHub release in Task C5.

- [ ] **Step 4: Build still clean**

Run: `dotnet build`

Expected: 0 warnings. Record the Velopack package version resolved.

- [ ] **Step 5: Commit**

```bash
git add src/Orbital.App/Orbital.App.csproj build/release.sh
git commit -m "Add Velopack NuGet and vpk pack step to release.sh"
```

---

### Task C5: `release.sh` — GitHub release

**Files:**
- Modify: `build/release.sh`

- [ ] **Step 1: Append GitHub release section**

Insert after the Velopack section:

```bash
# --- GitHub release ---
echo "▸ Composing release notes"
NOTES_FILE="$(mktemp)"
trap 'rm -f "$NOTES_FILE"' EXIT

PREV_TAG="$(git -C "$ROOT" describe --tags --abbrev=0 2>/dev/null || true)"
{
    echo "## Orbital $VERSION"
    echo
    if [ -n "$PREV_TAG" ]; then
        echo "Changes since $PREV_TAG:"
        echo
        git -C "$ROOT" log --pretty=format:'- %s' "$PREV_TAG..HEAD"
    else
        echo "Initial public release."
    fi
    echo
} > "$NOTES_FILE"

echo "▸ Creating GitHub release $TAG"
gh release create "$TAG" \
    --title "Orbital $VERSION" \
    --notes-file "$NOTES_FILE"

echo "▸ Uploading artifacts"
for f in "${MAC_ARTIFACTS[@]}"; do
    gh release upload "$TAG" "$f"
done
gh release upload "$TAG" "$WIN_ZIP"

# All Velopack artifacts (nupkg, RELEASES, Setup, Portable.zip)
find "$VPK_OUT" -maxdepth 2 -type f \( -name '*.nupkg' -o -name '*Setup*' -o -name 'RELEASES*' -o -name '*Portable.zip' \) \
    -exec gh release upload "$TAG" {} \;

echo "▸ Pushing tag"
git -C "$ROOT" tag "$TAG" 2>/dev/null || true
git -C "$ROOT" push origin "$TAG"

URL="$(gh release view "$TAG" --json url -q .url)"
echo "✓ Release complete: $URL"
```

- [ ] **Step 2: End-to-end dry-run check (without actually releasing)**

Without running the full script (which requires signing certs + a real tag), validate the script syntax:

```bash
bash -n build/release.sh && echo "syntax ok"
```

Expected: `syntax ok`.

- [ ] **Step 3: Commit**

```bash
git add build/release.sh
git commit -m "release.sh: create and upload GitHub release"
```

---

## Phase D — Velopack in-app updater

---

### Task D1: Bootstrap Velopack in `Program.Main`

**Files:**
- Modify: `src/Orbital.App/Program.cs`

- [ ] **Step 1: Add the `VelopackApp` call**

```csharp
// src/Orbital.App/Program.cs
namespace Orbital.App;

using Avalonia;
using Orbital.App.Services;
using Velopack;

internal sealed class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        // Velopack must run BEFORE any other startup logic. It handles the
        // --squirrel-install / --squirrel-uninstall / --squirrel-updated
        // subcommands Velopack injects and exits before we get here. For
        // normal launches it returns immediately.
        VelopackApp.Build().Run();

        using var guard = new SingleInstanceGuard();
        if (!guard.TryAcquire()) return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

- [ ] **Step 2: Build clean**

Run: `dotnet build`

Expected: 0 warnings, 0 errors. `Velopack` namespace resolves (added in C4).

- [ ] **Step 3: Smoke run**

Run: `timeout 3 dotnet run --project src/Orbital.App` (no-op exit after 3s).

Expected: starts, no Velopack error — we're running outside an installed context, so VelopackApp.Run returns without action.

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Program.cs
git commit -m "Initialize VelopackApp at the top of Main"
```

---

### Task D2: `IUpdateService` + `VelopackUpdateService`

**Files:**
- Create: `src/Orbital.App/Services/IUpdateService.cs`
- Create: `src/Orbital.App/Services/VelopackUpdateService.cs`

- [ ] **Step 1: Interface**

```csharp
// src/Orbital.App/Services/IUpdateService.cs
namespace Orbital.App.Services;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IUpdateService : IDisposable
{
    bool IsSupported { get; }
    bool IsUpdateAvailable { get; }
    string? AvailableVersion { get; }

    Task CheckAsync(CancellationToken ct = default);
    Task ApplyAndRestartAsync();

    event Action? UpdateAvailable;
    event Action? UpToDate;
    event Action<Exception>? UpdateFailed;
}
```

- [ ] **Step 2: Implementation**

```csharp
// src/Orbital.App/Services/VelopackUpdateService.cs
namespace Orbital.App.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

public sealed class VelopackUpdateService : IUpdateService
{
    private readonly ILogger<VelopackUpdateService> log;
    private readonly UpdateManager? manager;
    private readonly Timer? periodic;
    private UpdateInfo? pending;

    public bool IsSupported { get; }
    public bool IsUpdateAvailable => pending is not null;
    public string? AvailableVersion => pending?.TargetFullRelease?.Version?.ToString();

    public event Action? UpdateAvailable;
    public event Action? UpToDate;
    public event Action<Exception>? UpdateFailed;

    public VelopackUpdateService(ILogger<VelopackUpdateService> log)
    {
        this.log = log;
        try
        {
            manager = new UpdateManager(new GithubSource(
                "https://github.com/radaiko/Orbital",
                accessToken: null,
                prerelease: false));
            IsSupported = manager.IsInstalled;
        }
        catch (Exception ex)
        {
            log.LogInformation(ex, "UpdateManager unavailable — likely running outside an installed build");
            IsSupported = false;
        }

        if (IsSupported)
        {
            // First check 10s after startup, then every 24h.
            periodic = new Timer(_ => _ = CheckAsync(), null,
                TimeSpan.FromSeconds(10), TimeSpan.FromHours(24));
        }
    }

    public async Task CheckAsync(CancellationToken ct = default)
    {
        if (!IsSupported || manager is null) return;
        try
        {
            var info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null || info.TargetFullRelease is null)
            {
                pending = null;
                UpToDate?.Invoke();
                return;
            }
            pending = info;
            log.LogInformation("Update available: {Version}", AvailableVersion);
            UpdateAvailable?.Invoke();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Update check failed");
            UpdateFailed?.Invoke(ex);
        }
    }

    public async Task ApplyAndRestartAsync()
    {
        if (!IsSupported || manager is null || pending is null) return;
        try
        {
            await manager.DownloadUpdatesAsync(pending).ConfigureAwait(false);
            manager.ApplyUpdatesAndRestart(pending);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Apply update failed");
            UpdateFailed?.Invoke(ex);
        }
    }

    public void Dispose() => periodic?.Dispose();
}
```

Type names: `UpdateManager`, `UpdateInfo`, `GithubSource`, `TargetFullRelease` are Velopack's API surface. Verify exact names during implementation (run `vpk --version`, check matching NuGet docs). If a name differs, adapt and note in your final report.

- [ ] **Step 3: Build clean**

Run: `dotnet build`

Expected: 0 warnings, 0 errors. If compile errors, they point to the Velopack API version mismatch — adjust the property/method names as the IDE suggests.

- [ ] **Step 4: Commit**

```bash
git add src/Orbital.App/Services/IUpdateService.cs src/Orbital.App/Services/VelopackUpdateService.cs
git commit -m "Add IUpdateService and Velopack-backed implementation"
```

---

### Task D3: Tray menu update state

**Files:**
- Modify: `src/Orbital.App/Services/TrayIconController.cs`

- [ ] **Step 1: Add update state to the tray**

Add enum + field + events at the top of the class:

```csharp
public enum UpdateMenuState { None, Idle, Available, UpToDate }

private UpdateMenuState updateState = UpdateMenuState.None;
private string? availableUpdateVersion;

public event Action? CheckForUpdatesRequested;
public event Action? UpdateNowRequested;
```

Add method:

```csharp
public void SetUpdateState(UpdateMenuState state, string? version = null)
{
    updateState = state;
    availableUpdateVersion = version;
    if (trayIcon is null) return;
    RebuildMenuWithAccessibility(); // rebuild handles both accessibility and update
}
```

Modify `RebuildMenuWithAccessibility` (renamed for clarity; keep old name compatible):

```csharp
private void RebuildMenuWithAccessibility()
{
    if (trayIcon is null) return;
    var menu = BuildMenu();

    // Accessibility banner at the very top
    if (accessibilityDenied)
    {
        var item = new NativeMenuItem("⚠ Grant Accessibility…");
        item.Click += (_, _) => OpenAccessibilityPane();
        menu.Items.Insert(0, item);
        menu.Items.Insert(1, new NativeMenuItemSeparator());
    }

    // Update item just above the "About Orbital…" item (find it by header)
    InsertUpdateMenuItem(menu);

    trayIcon.Menu = menu;
}

private void InsertUpdateMenuItem(NativeMenu menu)
{
    if (updateState is UpdateMenuState.None) return;

    var item = new NativeMenuItem();
    switch (updateState)
    {
        case UpdateMenuState.Idle:
            item.Header = "Check for updates…";
            item.Click += (_, _) => CheckForUpdatesRequested?.Invoke();
            break;
        case UpdateMenuState.Available:
            item.Header = $"⬆ Update to v{availableUpdateVersion}";
            item.Click += (_, _) => UpdateNowRequested?.Invoke();
            break;
        case UpdateMenuState.UpToDate:
            item.Header = "✓ Up to date";
            item.IsEnabled = false;
            break;
    }
    // Insert just before the Quit separator — find the Quit item and insert above its separator
    // For simplicity, insert at position max(0, count-2) which sits just above Quit.
    var pos = Math.Max(0, menu.Items.Count - 2);
    menu.Items.Insert(pos, item);
    menu.Items.Insert(pos + 1, new NativeMenuItemSeparator());
}
```

- [ ] **Step 2: Build clean**

Run: `dotnet build`

- [ ] **Step 3: Commit**

```bash
git add src/Orbital.App/Services/TrayIconController.cs
git commit -m "Tray shows Check/Update/UpToDate states driven by controller"
```

---

### Task D4: Wire update service into `App`

**Files:**
- Modify: `src/Orbital.App/App.axaml.cs`
- Modify: `src/Orbital.App/Views/AboutWindow.axaml.cs` (wire About's "Check" button to the real service)

- [ ] **Step 1: Instantiate and wire in `App`**

Add field:

```csharp
private IUpdateService? updates;
```

In `InitializeAsync` after `dialogs = new DialogService();`:

```csharp
updates = new VelopackUpdateService(loggerFactory!.CreateLogger<VelopackUpdateService>());
updates.UpdateAvailable += OnUpdateAvailable;
updates.UpToDate += OnUpToDate;
updates.UpdateFailed += OnUpdateFailed;
```

Add after `tray.Show()`:

```csharp
// Populate tray update state at startup.
tray.CheckForUpdatesRequested += async () => await updates!.CheckAsync();
tray.UpdateNowRequested += async () => await updates!.ApplyAndRestartAsync();
if (updates.IsSupported)
    tray.SetUpdateState(TrayIconController.UpdateMenuState.Idle);
```

Handlers:

```csharp
private void OnUpdateAvailable()
{
    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        tray?.SetUpdateState(TrayIconController.UpdateMenuState.Available, updates?.AvailableVersion));
}

private void OnUpToDate()
{
    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        tray?.SetUpdateState(TrayIconController.UpdateMenuState.UpToDate);
        // Revert to Idle after 3s
        var timer = new System.Timers.Timer(3000) { AutoReset = false };
        timer.Elapsed += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                tray?.SetUpdateState(TrayIconController.UpdateMenuState.Idle));
            timer.Dispose();
        };
        timer.Start();
    });
}

private async void OnUpdateFailed(Exception ex)
{
    await (dialogs?.ShowErrorAsync("Update failed",
        $"Could not update Orbital: {ex.Message}\n\nSee the log file for details.") ?? Task.CompletedTask);
}
```

Add `updates` to the Quit disposal chain:

```csharp
tray.QuitRequested += async () =>
{
    await host!.FlushAsync();
    if (hotkeys is not null) await hotkeys.DisposeAsync();
    updates?.Dispose();
    host.Dispose();
    tray?.Dispose();
    desktop.Shutdown();
};
```

Also in `App.Dispose()` — add `updates?.Dispose();` before `host?.Dispose();`.

- [ ] **Step 2: Wire About's "Check" button to real service**

In `ShowAbout()`:

```csharp
w.CheckUpdatesRequested = () =>
{
    if (updates is null || !updates.IsSupported) return false;
    _ = updates.CheckAsync();
    // The return is indicative; actual UX is the status text updated on the returned handler,
    // but the sync bool return can't express "in flight" — simplest: return false here and let
    // the user see the tray menu reflect the result.
    return false;
};
```

For a cleaner UX, change `CheckUpdatesRequested` to `async Task<bool>` or make the About window observe `IUpdateService` directly. For MVP the above is acceptable.

- [ ] **Step 3: Build clean; commit**

Run: `dotnet build`

```bash
git add src/Orbital.App/App.axaml.cs src/Orbital.App/Views/AboutWindow.axaml.cs
git commit -m "Wire VelopackUpdateService to tray menu and About dialog"
```

---

## Phase E — Landing page

---

### Task E1: Static HTML + CSS structure

**Files:**
- Create: `docs/index.html`
- Create: `docs/.nojekyll`
- Modify: `docs/icon.svg` (already exists from A2 — leave as is)

- [ ] **Step 1: `.nojekyll`**

```bash
touch docs/.nojekyll
```

- [ ] **Step 2: Write `docs/index.html`**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Orbital — A keyboard-driven todo list for your desktop</title>
  <meta name="description" content="Local-first, keyboard-first todo list for macOS and Windows. Two hotkeys, drag-to-reorder, no cloud.">
  <link rel="icon" href="icon.svg" type="image/svg+xml">
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

    :root {
      --bg: #0a0a0a;
      --surface: #141414;
      --border: #262626;
      --text: #fafafa;
      --text-dim: #888;
      --accent: #3b82f6;
      --accent-hover: #60a5fa;
      --radius: 10px;
      --max-w: 960px;
    }

    html { scroll-behavior: smooth; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      background: var(--bg);
      color: var(--text);
      line-height: 1.6;
      -webkit-font-smoothing: antialiased;
    }
    a { color: var(--accent); text-decoration: none; }
    a:hover { color: var(--accent-hover); }

    .container { max-width: var(--max-w); margin: 0 auto; padding: 0 1.5rem; }

    header {
      position: sticky; top: 0; z-index: 10;
      background: rgba(10, 10, 10, 0.85);
      backdrop-filter: blur(12px);
      border-bottom: 1px solid var(--border);
    }
    .header-inner {
      display: flex; align-items: center; justify-content: space-between;
      height: 56px;
    }
    .logo { display: flex; align-items: center; gap: 0.625rem; }
    .logo img { width: 28px; height: 28px; border-radius: 6px; }
    .logo span { font-size: 1.125rem; font-weight: 600; letter-spacing: -0.01em; }
    .gh-link {
      display: flex; align-items: center; gap: 0.375rem;
      color: var(--text-dim); font-size: 0.875rem;
    }
    .gh-link:hover { color: var(--text); }
    .gh-link svg { width: 20px; height: 20px; fill: currentColor; }

    .hero { text-align: center; padding: 6rem 0 4rem; }
    .hero h1 {
      font-size: clamp(2.5rem, 6vw, 4rem);
      font-weight: 700;
      letter-spacing: -0.03em;
      line-height: 1.1;
    }
    .hero .tagline {
      margin-top: 1rem;
      font-size: clamp(1.125rem, 2.5vw, 1.375rem);
      color: var(--text-dim);
      font-style: italic;
    }
    .hero .sub {
      margin-top: 1rem;
      font-size: 1rem;
      color: var(--text-dim);
      max-width: 540px;
      margin-left: auto; margin-right: auto;
    }

    .cta-row { margin-top: 2.5rem; display: flex; flex-direction: column; align-items: center; gap: 0.75rem; }
    .btn-primary {
      display: inline-flex; align-items: center; gap: 0.5rem;
      padding: 0.75rem 1.75rem;
      background: var(--accent); color: #fff;
      border: none; border-radius: var(--radius);
      font-size: 1rem; font-weight: 500;
      cursor: pointer; transition: background 0.15s;
      text-decoration: none;
    }
    .btn-primary:hover { background: var(--accent-hover); color: #fff; }
    .btn-primary svg { width: 18px; height: 18px; }
    .version-badge { font-size: 0.8125rem; color: var(--text-dim); }

    .downloads { padding: 3rem 0; }
    .downloads h2 {
      text-align: center; font-size: 1.25rem; font-weight: 600;
      margin-bottom: 1.5rem; color: var(--text-dim);
    }
    .dl-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1rem;
    }
    .dl-card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: 1.25rem;
      text-align: center;
      transition: border-color 0.15s;
    }
    .dl-card:hover { border-color: var(--accent); }
    .dl-card.detected { border-color: var(--accent); }
    .dl-card .os-icon { font-size: 1.75rem; margin-bottom: 0.5rem; }
    .dl-card .os-name { font-size: 0.9375rem; font-weight: 500; }
    .dl-card .os-detail { font-size: 0.75rem; color: var(--text-dim); margin-top: 0.25rem; }
    .dl-card .dl-btn {
      display: inline-block; margin-top: 0.75rem;
      padding: 0.4rem 1rem;
      border: 1px solid var(--border);
      border-radius: 6px;
      font-size: 0.8125rem; color: var(--text);
      transition: border-color 0.15s, background 0.15s;
    }
    .dl-card .dl-btn:hover { border-color: var(--accent); background: rgba(59,130,246,0.1); color: var(--text); }
    .dl-card .dl-btn.unavailable { opacity: 0.4; pointer-events: none; }

    .features { padding: 4rem 0; }
    .features h2 {
      text-align: center; font-size: 1.5rem; font-weight: 600;
      margin-bottom: 2rem;
    }
    .feat-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 1.25rem;
    }
    .feat-card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: 1.5rem;
    }
    .feat-card .feat-icon { font-size: 1.5rem; margin-bottom: 0.5rem; }
    .feat-card h3 { font-size: 1rem; font-weight: 600; margin-bottom: 0.375rem; }
    .feat-card p { font-size: 0.875rem; color: var(--text-dim); line-height: 1.5; }

    .screenshots { padding: 3rem 0; }
    .shot-grid { display: grid; grid-template-columns: 1fr; gap: 1.5rem; }
    @media (min-width: 800px) { .shot-grid { grid-template-columns: 1fr 1fr; } }
    .shot-card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: 1rem;
      text-align: center;
    }
    .shot-card img {
      max-width: 100%; height: auto; border-radius: 6px; display: block;
      border: 1px solid var(--border);
    }
    .shot-card p {
      margin-top: 0.5rem; font-size: 0.875rem; color: var(--text-dim);
    }

    footer {
      border-top: 1px solid var(--border);
      padding: 2rem 0;
      text-align: center;
      font-size: 0.8125rem;
      color: var(--text-dim);
    }
    .footer-links { display: flex; justify-content: center; gap: 1.5rem; margin-bottom: 0.75rem; }
    .footer-links a { color: var(--text-dim); }
    .footer-links a:hover { color: var(--text); }

    @media (max-width: 600px) {
      .hero { padding: 4rem 0 3rem; }
      .dl-grid { grid-template-columns: 1fr 1fr; }
    }
  </style>
</head>
<body>

<header>
  <div class="container header-inner">
    <a href="#" class="logo">
      <img src="icon.svg" alt="Orbital icon" width="28" height="28">
      <span>Orbital</span>
    </a>
    <a href="https://github.com/radaiko/Orbital" class="gh-link" target="_blank" rel="noopener">
      <svg viewBox="0 0 16 16"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/></svg>
      GitHub
    </a>
  </div>
</header>

<main>
  <section class="hero container">
    <h1>Orbital</h1>
    <p class="tagline">A keyboard-driven todo list for your desktop.</p>
    <p class="sub">Two hotkeys. Local JSON. Nothing between you and your list.</p>
    <div class="cta-row">
      <a id="primary-cta" href="https://github.com/radaiko/Orbital/releases" class="btn-primary">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
        <span id="cta-label">Download</span>
      </a>
      <span id="version-badge" class="version-badge"></span>
    </div>
  </section>

  <section class="downloads container">
    <h2>All downloads</h2>
    <div class="dl-grid">
      <div class="dl-card" data-os="mac">
        <div class="os-icon">🍎</div>
        <div class="os-name">macOS</div>
        <div class="os-detail">Apple Silicon · notarized</div>
        <a class="dl-btn unavailable" href="#" data-asset="Orbital-osx-arm64">Download</a>
      </div>
      <div class="dl-card" data-os="win">
        <div class="os-icon">🪟</div>
        <div class="os-name">Windows</div>
        <div class="os-detail">x64 · portable</div>
        <a class="dl-btn unavailable" href="#" data-asset="Orbital-win-x64">Download</a>
      </div>
      <div class="dl-card" data-os="linux">
        <div class="os-icon">🐧</div>
        <div class="os-name">Linux</div>
        <div class="os-detail">Build from source</div>
        <a class="dl-btn" href="https://github.com/radaiko/Orbital#build-from-source">Instructions</a>
      </div>
    </div>
  </section>

  <section class="features container">
    <h2>What's inside</h2>
    <div class="feat-grid">
      <div class="feat-card"><div class="feat-icon">⌨️</div><h3>Keyboard-first</h3><p>Every action, every todo, every edit — no mouse required.</p></div>
      <div class="feat-card"><div class="feat-icon">🔒</div><h3>100% local</h3><p>No account, no cloud, no telemetry. Your data lives in a JSON file you own.</p></div>
      <div class="feat-card"><div class="feat-icon">🔀</div><h3>Drag to reorder</h3><p>No priority field. You rank things with your hands.</p></div>
      <div class="feat-card"><div class="feat-icon">🌗</div><h3>Theme-aware</h3><p>Follows your system's light/dark setting without fuss.</p></div>
      <div class="feat-card"><div class="feat-icon">🔄</div><h3>Auto-updates</h3><p>Background checks via GitHub Releases. Update when you choose.</p></div>
      <div class="feat-card"><div class="feat-icon">📥</div><h3>Quick add anywhere</h3><p><code>⌃⌥T</code> summons a popup. Type, Enter, gone.</p></div>
    </div>
  </section>

  <section class="screenshots container">
    <div class="shot-grid">
      <div class="shot-card"><img src="screenshot-overlay.png" alt="Orbital overlay window"><p>The overlay — always-on-top, keyboard-driven</p></div>
      <div class="shot-card"><img src="screenshot-quickadd.png" alt="Orbital quick-add popup"><p>The quick-add popup — title + natural-language date</p></div>
    </div>
  </section>
</main>

<footer>
  <div class="container">
    <div class="footer-links">
      <a href="https://github.com/radaiko/Orbital">GitHub</a>
      <a href="https://github.com/radaiko/Orbital/blob/main/LICENSE">License</a>
      <a href="https://github.com/radaiko/Orbital/releases">Releases</a>
    </div>
    <div>© radaiko — Orbital is open source, MIT-licensed.</div>
  </div>
</footer>

<script>
  // Populated in Task E2.
</script>
</body>
</html>
```

- [ ] **Step 3: Commit**

```bash
git add docs/index.html docs/.nojekyll
git commit -m "Add dark-themed landing page skeleton (HTML + CSS)"
```

---

### Task E2: Client-side JS for version + OS detection

**Files:**
- Modify: `docs/index.html` (replace the empty `<script>` block)

- [ ] **Step 1: Replace the script**

```html
<script>
(function () {
  const REPO = "radaiko/Orbital";
  const cta       = document.getElementById("primary-cta");
  const ctaLabel  = document.getElementById("cta-label");
  const versionEl = document.getElementById("version-badge");

  // OS detection
  const ua = navigator.userAgent || "";
  const platform = (navigator.userAgentData?.platform || navigator.platform || "").toLowerCase();
  const isMac   = /mac|darwin/.test(platform) || /mac|darwin/i.test(ua);
  const isWin   = /win/.test(platform)        || /windows/i.test(ua);
  const isLinux = !isMac && !isWin;

  const detectedOs = isMac ? "mac" : isWin ? "win" : "linux";
  const card = document.querySelector(`.dl-card[data-os="${detectedOs}"]`);
  if (card) card.classList.add("detected");
  ctaLabel.textContent = isMac ? "Download for macOS" : isWin ? "Download for Windows" : "Download";

  // Fetch latest release
  fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: { Accept: "application/vnd.github+json" } })
    .then(r => r.ok ? r.json() : Promise.reject(new Error("No releases yet")))
    .then(data => {
      const tag     = data.tag_name;
      const version = tag.replace(/^v/, "");
      versionEl.textContent = `v${version}`;

      // Map asset-prefix → href
      const byPrefix = {};
      (data.assets || []).forEach(a => {
        const url = a.browser_download_url;
        if (!url) return;
        if (url.includes("Orbital-osx-arm64-") && url.endsWith(".zip")) byPrefix["Orbital-osx-arm64"] = url;
        if (url.includes("Orbital-win-x64-")   && url.endsWith(".zip")) byPrefix["Orbital-win-x64"]   = url;
      });

      // Card buttons
      document.querySelectorAll(".dl-card .dl-btn[data-asset]").forEach(btn => {
        const prefix = btn.getAttribute("data-asset");
        const href = byPrefix[prefix];
        if (href) {
          btn.href = href;
          btn.classList.remove("unavailable");
          btn.textContent = "Download";
        }
      });

      // Primary CTA → OS's download (if available)
      const ctaKey = isMac ? "Orbital-osx-arm64" : isWin ? "Orbital-win-x64" : null;
      if (ctaKey && byPrefix[ctaKey]) cta.href = byPrefix[ctaKey];
    })
    .catch(() => {
      // Pre-first-release: fall back to the releases index
      cta.href = `https://github.com/${REPO}/releases`;
      versionEl.style.display = "none";
    });
})();
</script>
```

- [ ] **Step 2: Local preview**

Run:
```bash
python3 -m http.server 8080 -d docs/ &
PID=$!
sleep 1
echo "Opening http://localhost:8080/ — close when done (Ctrl+C to kill server)"
open http://localhost:8080/
```

Visually verify: page loads, OS-appropriate card highlighted, cards show "Download" (likely "unavailable" styling since no release yet), version badge hidden.

Stop the server: `kill $PID` (or Ctrl+C).

- [ ] **Step 3: Commit**

```bash
git add docs/index.html
git commit -m "Landing page: populate downloads and version from GitHub API"
```

---

### Task E3: Placeholder screenshots + activate GitHub Pages

**Files:**
- Create: `docs/screenshot-overlay.png` (placeholder)
- Create: `docs/screenshot-quickadd.png` (placeholder)

- [ ] **Step 1: Create placeholder PNGs**

Until the app is running and screenshots are captured, commit 1×1 transparent PNGs (so the `<img>` tags don't render broken icons). Replace after F3.

```bash
python3 - <<'PY'
import struct, zlib, pathlib
def png(rgba, w, h):
    def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d))
    sig  = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    raw  = b"".join(b"\x00" + bytes(rgba) * w for _ in range(h))
    idat = zlib.compress(raw, 9)
    return sig + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")
# 1440x900 near-black placeholder
data = png([20,20,20,255], 1440, 900)
pathlib.Path("docs/screenshot-overlay.png").write_bytes(data)
pathlib.Path("docs/screenshot-quickadd.png").write_bytes(data)
PY
```

- [ ] **Step 2: Commit**

```bash
git add docs/screenshot-overlay.png docs/screenshot-quickadd.png
git commit -m "Add placeholder screenshots (to be replaced after first release)"
```

- [ ] **Step 3: Enable GitHub Pages (manual via gh CLI)**

The first time only. Run:

```bash
gh api -X POST \
  -H "Accept: application/vnd.github+json" \
  "/repos/radaiko/Orbital/pages" \
  -f "source[branch]=main" \
  -f "source[path]=/docs" 2>&1 || \
gh api -X PUT \
  -H "Accept: application/vnd.github+json" \
  "/repos/radaiko/Orbital/pages" \
  -f "source[branch]=main" \
  -f "source[path]=/docs"
```

(The create API returns 409 if Pages is already enabled; the PUT form updates source. One of the two will succeed.)

- [ ] **Step 4: Push and verify**

```bash
git push origin main
```

Wait ~1 minute, then visit `https://radaiko.github.io/Orbital/` — should show the landing page.

---

## Phase F — First release

---

### Task F1: Add MIT LICENSE

**Files:**
- Create: `LICENSE`

- [ ] **Step 1: MIT LICENSE text**

```
MIT License

Copyright (c) 2026 radaiko

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 2: Commit and push**

```bash
git add LICENSE
git commit -m "Add MIT LICENSE"
git push origin main
```

---

### Task F2: Create `.env` locally (manual, one-time)

**Files:**
- Create: `.env` (gitignored — NOT committed)

- [ ] **Step 1: Copy template and fill in**

```bash
cp build/release-env.sample .env
$EDITOR .env  # or: nano .env, open -a TextEdit .env
```

Fill in `APPLE_ID`, `APPLE_TEAM_ID`, `APPLE_PASSWORD` (app-specific), `CODESIGN_IDENTITY`.

- [ ] **Step 2: Verify `.env` is gitignored**

Run: `git status`

Expected: `.env` does NOT appear in untracked or staged files.

---

### Task F3: Run the first release

**Files:** none (script execution)

- [ ] **Step 1: Run `release.sh`**

```bash
./build/release.sh
```

Expected: full pipeline runs, the macOS bundle is signed + notarized (stapled), Windows zip built, Velopack artifacts created, GitHub release `v1.0.0` published, tag pushed. Final line prints the release URL.

- [ ] **Step 2: Download and test the released macOS bundle**

```bash
mkdir -p /tmp/orbital-release-test
cd /tmp/orbital-release-test
gh release download v1.0.0 -R radaiko/Orbital -p "Orbital-osx-arm64-1.0.0.zip"
ditto -xk Orbital-osx-arm64-1.0.0.zip .
open Orbital.app
```

Expected: app opens without the "unidentified developer" dialog (proves notarization worked). Verify the tray icon appears with the real orbital logo.

- [ ] **Step 3: Capture real screenshots**

Launch the app, open the overlay, open quick-add. Screenshot each with `Cmd+Shift+4`, save as:

- `/Users/radaiko/dev/private/Orbital/docs/screenshot-overlay.png`
- `/Users/radaiko/dev/private/Orbital/docs/screenshot-quickadd.png`

Replace the placeholder files.

- [ ] **Step 4: Commit real screenshots**

```bash
cd /Users/radaiko/dev/private/Orbital
git add docs/screenshot-overlay.png docs/screenshot-quickadd.png
git commit -m "Replace placeholder screenshots with real captures"
git push origin main
```

- [ ] **Step 5: Verify landing page**

Wait ~1 minute for GitHub Pages redeploy. Visit `https://radaiko.github.io/Orbital/`.

Expected: version badge shows `v1.0.0`, both download cards for macOS and Windows show working "Download" links pointing at the v1.0.0 assets, OS-detection highlights the right card, screenshots render.

- [ ] **Step 6: Smoke-test Velopack update flow**

Bump `Directory.Build.props` Version to `1.0.1`. Run `./build/release.sh` again to publish v1.0.1. Then, on the installed v1.0.0 (from step 2), click the tray's "Check for updates…" — tray should change to "⬆ Update to v1.0.1". Click → app updates and relaunches.

If this step fails, the log file (`~/Library/Application Support/Orbital/logs/latest.log`) contains the Velopack error. Diagnose, fix, re-release.

- [ ] **Step 7: Update the plan / spec with anything learned**

If Velopack API names or release script assumptions needed adjustment, reflect it in this plan file + the design spec and commit with message `docs: fold release-time learnings into spec and plan`.

---

## Self-review summary

Checked against `docs/superpowers/specs/2026-04-23-orbital-release-updater-design.md`:

- **§4.1 Real app icon:** Tasks A1, A2, A3 cover it.
- **§4.2 Versioning:** B1 sets the props; release.sh (C2) reads them.
- **§4.3 About dialog:** B8.
- **§4.4 File logging:** B2 + B3.
- **§4.5 Error dialogs:** B4 + B5; update-failure wired in D4.
- **§4.6 macOS Accessibility flow:** B9.
- **§4.7 Theme correctness:** B6.
- **§4.8 First-run welcome todo:** B7.
- **§5 Branding pipeline:** A1 + A2 (+ A3 for Windows ICO).
- **§6 Release pipeline:** C1–C5.
- **§7 Velopack updater:** D1–D4.
- **§8 Landing page:** E1–E3.
- **§9 Testing / manual verification:** F2, F3.
- **§10 Open items:** MIT LICENSE is F1; remaining items (robust Accessibility detection, delta updates, light-mode landing page) remain deferred by design.

**No placeholders found.** Method signatures used match across tasks (`IUpdateService`, `TrayIconController.SetUpdateState`, `UpdateMenuState` enum, `ISettingsStore.LoadWithProvenanceAsync`).
