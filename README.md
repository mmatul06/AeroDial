# AeroDial

**A radial launcher overlay for Windows.**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2B-blue)](https://github.com/mmatul06/AeroDial)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)
![Version](https://img.shields.io/github/v/release/mmatul06/AeroDial)
![Downloads](https://img.shields.io/github/downloads/mmatul06/AeroDial/total)

AeroDial opens a customisable radial menu wherever your cursor is, triggered by any key or mouse button, letting you launch apps, open folders, run commands, fire key combos, run multi-step keyboard macros, control media, paste clipboard snippets, and navigate nested submenus without touching your taskbar. It can swap to a different menu per app (or stay out of an app entirely), show the currently-playing track, be driven from the keyboard, and it works on top of any application including fullscreen games, across any number of monitors at any DPI scale.

---
## Screenshots

![Overlay](Screenshot/Themes.png)

---

## Download

**[⬇ Download AeroDial v3.0.2](https://github.com/mmatul06/AeroDial/releases/latest)**

Download `AeroDial-3.0.2-Setup.exe` and run it. The installer is per user, so it needs no admin rights, and AeroDial appears in Apps & features like any other program.

AeroDial starts silently in the system tray. Double Click on System Tray icon to open the Settings window.

---

## Features

### Trigger
- Any keyboard key, mouse button or modifier combo
- Hold mode: hold to show, release to select
- Toggle mode: press to open, press again (or click) to close
- Modifier filter: only trigger when Ctrl, Shift, Alt, or Win is held
- **Tap-through** (mouse triggers, Hold mode): a quick tap is passed to the app as a normal click, so middle-click and the back/forward buttons keep working in browsers; holding opens the dial
- **Pause AeroDial** from the tray menu when you want the trigger button back temporarily

### Menu
- Radial ring with 3-12 slices per level
- Nested submenus: hover a submenu slice to expand a child ring; center-click to go back
- Empty slice slots rendered at reduced opacity so the ring always looks complete
- Configurable center gap (0-40 px) to detach slices from the inner ring

### Selection modes
- Hover dwell: cursor dwell time triggers the action
- Click: left-click a slice
- Flick: cursor angle from center determines the aimed slice; execute on trigger release or second press
- **Keyboard**: while the dial is open, arrows move the highlight (child rings open as you go), 1-9 pick a slice, Enter runs, Backspace goes back, Esc closes

### Actions

| Action | Description |
|---|---|
| Launch app | Start any executable or shortcut with optional arguments |
| Open folder | Open a folder in File Explorer (a file path selects that file) |
| Run command | Anything you would type into Win+R: `regedit`, `ms-settings:display`, `cmd /k dir`, `shell:startup`, `%APPDATA%`; optional run as administrator |
| Open URL | Open any URL in the default browser |
| Key combo | Send any keystroke combination (e.g. Win+D, Ctrl+Shift+T) |
| Macro | Run an ordered sequence of keystrokes, typed text, and delays |
| Media control | Play/Pause, Next, Previous, Volume Up/Down, Mute |
| Run script | Execute .bat, .cmd or .ps1 scripts |
| Paste text | Set clipboard text and paste it |
| Submenu | Open a nested child ring |
| Focus window | Bring an open window to the foreground (Active Apps) |

Actions that launch things run after the ring has closed, on a background thread, so the dial never waits for a slow app to start.

### Macros
Chain multiple steps into one slice — for example, type `FILLET` then press `Enter` in a CAD app, or hold `Shift` across several keystrokes:

- **Type text** — types a literal string (sent as Unicode, so it's keyboard-layout independent)
- **Press key** — a single key or chord (e.g. `Enter`, `Tab`, `Ctrl+S`)
- **Key down / Key up** — hold a key across later steps and release it when you want
- **Delay** — wait a set number of milliseconds between steps

Build macros step-by-step in the menu editor. Keystrokes land in whatever app was focused before the dial opened.

### App profiles (context-aware menus)
Bind a specific menu to a specific app. When that app is in the foreground and you open the dial, it shows the assigned menu instead of the default — a CAD dial for AutoCAD, an editing dial for Photoshop, and so on. Set these up in **Settings → App profiles** (with an "add from running app" picker); other apps fall back to the default menu. A profile can also be set to **Disabled**, so the dial stays out of a game or an app that uses the trigger button itself and the button passes straight through.

### Dynamic submenus (built automatically, no setup needed)
- **Active Apps** (`__active_tasks__`) -- live list of open windows with per-app icons, built in the background on every open
- **Clipboard History** (`__clipboard_history__`) -- up to 8 recent clipboard text entries (offers to enable Windows clipboard history if it is off)

### Visuals
- Radial gradient fills, blur glow on hover, inner accent arc
- 29 built-in themes, plus **Auto (Windows accent)** which follows your desktop accent color:
  - *Dark:* Obsidian, Graphite, Onyx Ice, Nord, Tokyo Night, Midnight Teal, Rose Pine, Solarized Dark, Dusk, Ocean, Matrix, Aurora
  - *Rich colour:* Royal Gold, Copper, Ember, Sunset, Crimson, Neon, Sakura, Cyberpunk, Synthwave, Ultraviolet
  - *Light:* Chalk, Arctic, Porcelain, Champagne, Ink
  - *Other:* Glass (smoked, the desktop shows through) and High Contrast (accessibility)
- Full custom theme support: JSON files in `%AppData%\AeroDial\themes\`
- Theme editor in Settings with a live preview: duplicate any built-in theme and edit its 18 color fields with color-picker flyouts
- Smooth ease-out open/close animations; respects Windows animation preference
- Per-pixel transparency via DWM; the ring is only re-rasterized when something changes

### Now playing
- Shows the currently-playing track title below the ring while media is playing
- An optional theme-coloured audio visualizer that pulses with the volume level
- Updates live when you change tracks (from the dial or anywhere) — reads the real Windows media session, so it works with any player that integrates with Windows (Spotify, Chrome/Edge/Firefox, Groove, VLC, and more)
- Both are toggleable in **Settings → Appearance**

### Scroll wheel
- Scroll wheel captured while overlay is open
- Each slice can bind scroll-up and scroll-down to independent media actions (volume, track, etc.)

### Icons
- Icons come from the Windows system icon font (Segoe Fluent Icons; Segoe MDL2 Assets on Windows 10): a searchable picker of 120 named icons, and any glyph by hex code (`fluent:E8B7`), tinted per theme
- Exe icon extraction for Launch app items and Active Apps
- Custom icons: any .png, .jpg, .ico, .bmp file

### Settings window
- Windows 11 native look: Mica backdrop, navigation rail, follows light/dark mode and your accent color
- Eight pages: Trigger, Appearance, Behavior, Menus, App profiles, Themes, Advanced, About
- Export / Import all menus, profiles, settings and custom themes as one file (Advanced page)
- Optional daily update check with a tray notice

### System tray
- No taskbar presence; always accessible from the tray icon
- Right-click: Settings, Pause AeroDial, About, Quit
- Double-click: open Settings
- Settings window hides to tray when closed (X); restore by double-clicking tray icon
- One-time "AeroDial is running" hint on first launch

---

## System requirements

- Windows 10 version 2004 (build 19041) or later
- Windows 11 recommended for best visual results
- x64 CPU
- .NET 9 runtime (bundled in release builds -- no separate install needed)

---

## Installation

1. Download `AeroDial-3.0.2-Setup.exe` from [Releases](../../releases)
2. Run it and follow the wizard. Installing takes a few seconds and needs **no admin rights**: AeroDial installs for your user only, under `%LocalAppData%\Programs\AeroDial`
3. Leave **Start AeroDial automatically when I sign in** ticked if you want it always available
4. AeroDial starts silently in the system tray
5. Right-click the tray icon and choose **Settings** to configure your trigger and menus

Upgrading is the same download: the installer replaces the previous version in place and keeps your menus, profiles and themes. Close AeroDial from the tray first, otherwise Setup will ask you to.

The .NET runtime, the WinUI 3 native libraries and the 11 built-in themes are all inside the app, so there is nothing else to install.

To uninstall: **Settings > Apps > Installed apps > AeroDial > Uninstall**, or the Start menu shortcut. Your menus and themes in `%AppData%\AeroDial` are kept unless you answer Yes when the uninstaller offers to remove them.

> Prefer a portable copy? The installed `AeroDial.exe` is fully self-contained: copy it anywhere and run it directly.

---

## Usage

### First run
The default trigger is **Middle Mouse Button**. Hold it anywhere on the desktop or in an app and the radial menu opens at your cursor (a quick tap still middle-clicks as usual).

- **Hover** a slice to highlight it (and auto-expand any submenu slice)
- **Left-click** a slice to execute the action (in Click mode)
- **Left-click the center circle** to go back in a submenu, or close the menu at root
- **Right-click** anywhere outside the ring (or press Esc) to dismiss without acting
- **Keyboard**: arrows move, 1-9 pick a slice, Enter runs, Backspace goes back

### Changing the trigger
Open Settings (tray right-click) → **Trigger** → click **Record key or button**, then press your desired key or mouse button.

### Adding menu items
Settings → **Menus**. The ring preview *is* the editor:

- **Click a `+` slot** on the ring to add an item there
- **Click a slice** to edit its label, icon, and action
- **Drag a slice** onto another slot to move or swap it
- **Remove** leaves an empty slot in place, so you control exactly where each item and gap sits
- Click a submenu slice's **"Open / edit this submenu"** to drill in; use the breadcrumb to climb back
- Edits stay in a working copy — **Save** commits them, **Discard** reverts

### Changing the theme
Settings → **Themes** → select a theme and click **Apply**. Click **Duplicate** to make an editable copy of a built-in theme; the editor on the right previews changes live.

---

## Configuration

- Config file: `%AppData%\Roaming\AeroDial\config.json` (the previous version is kept as `config.json.bak`; the file carries a `configVersion` and older files are migrated automatically)
- Log file: `%AppData%\Roaming\AeroDial\aerodial.log`
- User themes: `%AppData%\Roaming\AeroDial\themes\`
- Built-in themes: compiled into the app (no external files needed); a `themes\` folder next to `AeroDial.exe` is also loaded if present
- Backup: Settings → Advanced → **Export settings** writes menus, profiles, settings and custom themes to one file; **Import settings** restores it

If the config cannot be read, it is set aside as `config.corrupt-<timestamp>.json` and defaults are recreated.

---

## Building from source

**Prerequisites:** .NET 9 SDK, Visual Studio 2022 with the Windows App SDK workload (or just the .NET 9 SDK for CLI builds).

```bash
git clone https://github.com/mmatul06/AeroDial.git
cd AeroDial
dotnet build AeroDial.sln -c Debug
dotnet test tests/AeroDial.Tests/AeroDial.Tests.csproj -c Debug -p:Platform=x64
```

Output: `src/AeroDial/bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64/`

`AeroDial.exe --selftest` opens the overlay, drives it with a virtual cursor and keyboard, walks every settings page, saves screenshots to `%AppData%\AeroDial\selftest\`, and exits. Use it to verify a build.

**Note:** `WindowsAppSDKSelfContained=true` and `SelfContained=true` are required in the csproj -- do not remove them or the app will crash with `ExecutionEngineException` on startup.

**Note:** clone into a short path (`C:\src\AeroDial`, not a deeply nested folder). The Windows App SDK resource step writes into `obj\...\MsixContent\` and fails with `PRI180: 0x80070057 ... does not exist` when the resulting path passes the Windows 260-character limit.

Your menus, app profiles, settings and any custom themes live in `%AppData%\AeroDial`, not in the repo. To carry them to another machine use **Settings > Advanced > Export settings**, then import the file there.

---

## Publishing a release build

```powershell
.\installer\build-installer.ps1
```

That publishes the app and compiles the installer, producing `dist/AeroDial-<version>-Setup.exe` (~102 MB). It needs [Inno Setup 6](https://jrsoftware.org/isdl.php). Pass `-NoPublish` to compile the installer from an existing publish output. Upload the setup exe to Releases.

The app alone is built with:

```bash
dotnet publish src/AeroDial/AeroDial.csproj -c Release -r win-x64
```

Output: `src/AeroDial/bin/Release/net9.0-windows10.0.26100.0/win-x64/publish/AeroDial.exe`

This is a **single self-contained executable** (~110 MB, compressed). The .NET runtime, the WinUI 3 native DLLs, and SkiaSharp are all bundled and self-extracted at runtime; the 11 built-in themes are compiled into the app, so no side files are needed.

The installer script `installer/AeroDial.iss` reads its version straight from the published exe, so the release version only ever lives in the csproj. It installs per user (the "start with Windows" setting writes to `HKCU\...\Run`, which a per-machine install would break for other users), refuses to run on anything below Windows 10 2004 or non-x64, detects a running AeroDial through its single-instance mutex, and on uninstall clears its own autostart entry and offers to remove `%AppData%\AeroDial`.

The single-file settings live in the csproj Release `PropertyGroup` (`PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`) plus `EnableMsixTooling=true`, so the plain `dotnet publish -c Release -r win-x64` above produces the single exe with no extra flags.

> **Trimming is intentionally disabled.** AeroDial relies on reflection-based `System.Text.Json` (config, themes, menus) and built-in COM (`AudioService`'s WASAPI volume access); enabling `PublishTrimmed` turns on .NET feature switches that disable both and breaks config load/save and audio at runtime. Do not add `-p:PublishTrimmed=true`.

---

## Project structure

```
AeroDial/
├── src/AeroDial.Core/  # Models and pure logic: config model + migrations, key-combo parsing,
│                       # ring geometry, profile matching, icon glyph catalog, accent theme builder
├── src/AeroDial/
│   ├── Core/           # Logger, Win32 P/Invoke, hook service, audio, media info, self-test
│   ├── Config/         # Config load/save/export/import
│   ├── Themes/         # Theme service and built-in presets
│   ├── Overlay/        # SkiaSharp renderer, Win32 overlay window, controller, icon registry
│   ├── Actions/        # Action dispatcher (launch, folders, commands, keys, media, scripts...)
│   └── UI/             # WinUI 3 settings window (one file per page), about dialog, tray service
├── tests/AeroDial.Tests/ # xUnit tests for AeroDial.Core
├── themes/             # Bundled theme JSON files
└── docs/               # Release notes and documentation assets
```

---

## License

This project is licensed under the [MIT License](LICENSE).

© 2026 Muhtasim Mahbub. All rights reserved.

---

## Author

**Muhtasim Mahbub**  
3M Design Solutions  
🌐 [3mdesignsolutions.com](https://3mdesignsolutions.com)  
📧 [3mdsolutions25@gmail.com](mailto:3mdsolutions25@gmail.com)

---

*If AeroDial is useful to you, consider giving it a ⭐ on GitHub!*

AeroDial is a sibling project to [MuteMaster](https://github.com/mmatul06/MuteMaster).
