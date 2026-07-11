# AeroDial

**A radial launcher overlay for Windows.**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2B-blue)](https://github.com/mmatul06/AeroDial)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)
![Version](https://img.shields.io/github/v/release/mmatul06/AeroDial)
![Downloads](https://img.shields.io/github/downloads/mmatul06/AeroDial/total)

AeroDial opens a customisable radial menu wherever your cursor is, triggered by any key or mouse button, letting you launch apps, fire key combos, run multi-step keyboard macros, control media, paste clipboard snippets, and navigate nested submenus without touching your taskbar. It can swap to a different menu per app, show the currently-playing track, and it works on top of any application including fullscreen games, across any number of monitors at any DPI scale.

---
## Screenshots

![Overlay](Screenshot/Themes.png)

---

## Download

**[⬇ Download AeroDial v2.0.0](https://github.com/mmatul06/AeroDial/releases/latest)**

Download and run `AeroDial.exe` (a single self-contained executable). No installer required.

AeroDial starts silently in the system tray. Double Click on System Tray icon to open the Settings window.

---

## Features

### Trigger
- Any keyboard key, mouse button or modifier combo
- Hold mode: hold to show, release to select
- Toggle mode: press to open, press again (or click) to close
- Modifier filter: only trigger when Ctrl, Shift, Alt, or Win is held

### Menu
- Radial ring with 4-12 slices per level
- Nested submenus: hover a submenu slice to expand a child ring; center-click to go back
- Empty slice slots rendered at reduced opacity so the ring always looks complete
- Configurable center gap (0-40 px) to detach slices from the inner ring

### Selection modes
- Hover dwell: cursor dwell time triggers the action
- Click: left-click a slice
- Flick: cursor angle from center determines the aimed slice; execute on trigger release or second press

### Actions

| Action | Description |
|---|---|
| Launch app | Start any executable with optional arguments |
| Open URL | Open any URL in the default browser |
| Key combo | Send any keystroke combination (e.g. Win+D, Ctrl+Shift+T) |
| Macro | Run an ordered sequence of keystrokes, typed text, and delays |
| Media | Play/Pause, Next, Previous, Volume Up/Down, Mute |
| Run script | Execute .bat or .ps1 scripts |
| Paste clipboard | Set clipboard text and paste it |
| Submenu | Open a nested child ring |
| Focus window | Bring an open window to the foreground |

### Macros
Chain multiple steps into one slice — for example, type `FILLET` then press `Enter` in a CAD app, or hold `Shift` across several keystrokes:

- **Type text** — types a literal string (sent as Unicode, so it's keyboard-layout independent)
- **Press key** — a single key or chord (e.g. `Enter`, `Tab`, `Ctrl+S`)
- **Key down / Key up** — hold a key across later steps and release it when you want
- **Delay** — wait a set number of milliseconds between steps

Build macros step-by-step in the menu editor. Keystrokes land in whatever app was focused before the dial opened.

### App profiles (context-aware menus)
Bind a specific menu to a specific app. When that app is in the foreground and you open the dial, it shows the assigned menu instead of the default — a CAD dial for AutoCAD, an editing dial for Photoshop, and so on. Set these up in **Settings → App Profiles** (with an "add from running app" picker); other apps fall back to the default menu.

### Dynamic submenus (built automatically, no setup needed)
- **Active Tasks** (`__active_tasks__`) -- live list of open windows with per-app icons, rebuilt on every open
- **Clipboard History** (`__clipboard_history__`) -- up to 8 recent clipboard text entries

### Visuals
- Radial gradient fills, blur glow on hover, inner accent arc
- 11 built-in themes: Obsidian, Ember, Midnight Teal, Chalk, Neon, Cyberpunk, Ocean, Sunset, Matrix, Arctic, Sakura
- Full custom theme support: JSON files in `%AppData%\AeroDial\themes\`
- Theme Editor in Settings: create themes with 17 color fields and color-picker flyouts
- Smooth ease-out open/close animations; respects Windows animation preference
- Per-pixel transparency via DWM

### Now playing
- Shows the currently-playing track title below the ring while media is playing
- An optional theme-coloured audio visualizer that pulses with the volume level
- Updates live when you change tracks (from the dial or anywhere) — reads the real Windows media session, so it works with any player that integrates with Windows (Spotify, Chrome/Edge/Firefox, Groove, VLC, and more)
- Both are toggleable in **Settings → Appearance**

### Scroll wheel
- Scroll wheel captured while overlay is open
- Each slice can bind scroll-up and scroll-down to independent media actions (volume, track, etc.)

### Input icons
- 40+ built-in programmatic icons (white, tinted per-theme at render time)
- Exe icon extraction for Launch App items and Active Tasks
- Custom icons: any .png, .jpg, .ico, .bmp file

### System tray
- No taskbar presence; always accessible from the tray icon
- Right-click: Settings, About, Quit
- Double-click: open Settings
- Settings window hides to tray when closed (X); restore by double-clicking tray icon

---

## System requirements

- Windows 10 version 2004 (build 19041) or later
- Windows 11 recommended for best visual results
- x64 CPU
- .NET 9 runtime (bundled in release builds -- no separate install needed)

---

## Installation

1. Download `AeroDial.exe` (v2.0.0) from [Releases](../../releases)
2. Run it — it's a **single self-contained executable**: no installer, no extraction, no separate .NET runtime
3. AeroDial starts silently in the system tray
4. Right-click the tray icon and choose **Settings** to configure your trigger and menus

No admin rights or registry writes are needed (other than the optional "start with Windows" toggle).

To uninstall: quit from the tray, delete `AeroDial.exe`, optionally delete `%AppData%\Roaming\AeroDial` where config and user themes are stored.

---

## Usage

### First run
The default trigger is **Middle Mouse Button**. Press it anywhere on the desktop or in an app and the radial menu opens at your cursor.

- **Hover** a slice to highlight it (and auto-expand any submenu slice)
- **Left-click** a slice to execute the action (in Click mode)
- **Left-click the center circle** to go back in a submenu, or close the menu at root
- **Right-click** anywhere outside the ring (or press Esc) to dismiss without acting

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
Settings → **Themes** → click **Apply** next to any theme.

---

## Configuration

- Config file: `%AppData%\Roaming\AeroDial\config.json`
- Log file: `%AppData%\Roaming\AeroDial\aerodial.log`
- User themes: `%AppData%\Roaming\AeroDial\themes\`
- Built-in themes: compiled into the app (no external files needed); a `themes\` folder next to `AeroDial.exe` is also loaded if present

If the config is corrupt, delete `config.json` and restart -- the app recreates defaults automatically.

---

## Building from source

**Prerequisites:** .NET 9 SDK, Visual Studio 2022 with the Windows App SDK workload (or just the .NET 9 SDK for CLI builds).

```bash
git clone https://github.com/mmatul06/AeroDial.git
cd AeroDial
dotnet build src/AeroDial/AeroDial.csproj -c Debug
```

Output: `src/AeroDial/bin/Debug/net9.0-windows10.0.26100.0/win-x64/`

**Note:** `WindowsAppSDKSelfContained=true` and `SelfContained=true` are required in the csproj -- do not remove them or the app will crash with `ExecutionEngineException` on startup.

---

## Publishing a release build

```bash
dotnet publish src/AeroDial/AeroDial.csproj -c Release -r win-x64
```

Output: `src/AeroDial/bin/Release/net9.0-windows10.0.26100.0/win-x64/publish/AeroDial.exe`

This is a **single self-contained executable** (~110 MB, compressed). The .NET runtime, the WinUI 3 native DLLs, and SkiaSharp are all bundled and self-extracted at runtime; the 11 built-in themes are compiled into the app, so no side files are needed. Upload the `.exe` directly to Releases.

The single-file settings live in the csproj Release `PropertyGroup` (`PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`) plus `EnableMsixTooling=true`, so the plain `dotnet publish -c Release -r win-x64` above produces the single exe with no extra flags.

> **Trimming is intentionally disabled.** AeroDial relies on reflection-based `System.Text.Json` (config, themes, menus) and built-in COM (`AudioService`'s WASAPI volume access); enabling `PublishTrimmed` turns on .NET feature switches that disable both and breaks config load/save and audio at runtime. Do not add `-p:PublishTrimmed=true`.

---

## Project structure

```
AeroDial/
├── src/AeroDial/
│   ├── Core/           # Constants, logger, extensions, Win32 P/Invoke, hook service
│   ├── Config/         # JSON config model and load/save service
│   ├── Themes/         # Theme model, service, and built-in presets
│   ├── Overlay/        # SkiaSharp renderer, Win32 overlay window, controller
│   ├── Actions/        # Action dispatcher (launch, keys, media, scripts...)
│   └── UI/             # WinUI 3 settings window, about dialog, tray service
├── themes/             # Bundled theme JSON files
└── docs/               # Screenshots and documentation assets
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
