## AeroDial v3.0.1

Fixes for the four issues reported right after 3.0.0.

### Fixed

- **Tap-through froze the mouse for about a second.** The replayed click was sent from inside the low-level mouse hook, so Windows held all mouse input until its hook timeout expired. The replay now runs on a worker thread and the hook returns immediately. The trigger gate for "Disabled" app profiles no longer touches `System.Diagnostics.Process` on the hook thread either, and the hook callbacks no longer allocate per mouse move. With debug logging on, any hook callback slower than 20 ms is reported in the log.
- **Icon-font glyphs were clipped** (the left edge of every icon was missing on the ring and in the menu editor). The glyph was being centered twice. The self-test now checks that rendered glyphs are whole and centered.
- **"Slices per ring" slider** sat in the middle of the page and moved when the window was resized. It now lines up with the other sliders.
- **Themes page** reworked: a divider and real spacing between the list and the editor, Apply / Duplicate / Delete buttons that fit the column, compact hex fields grouped by what they affect, the three numeric properties stacked as spin boxes, and the live preview, name, font and save buttons in a column that stays visible while you scroll the colors.

---

## AeroDial v3.0.0

The biggest AeroDial release yet: a rebuilt input and rendering core, a Windows 11 native settings window, a system icon font with hundreds of icons, two new action types, and a long list of quality-of-life fixes reported by users.

---

### Fixed

- **Crash when choosing an icon or app path** in the menu editor. The file picker threw inside an async handler and took the whole app down without a log entry. Pickers now use the Windows App SDK picker API, every handler is guarded, and XAML exceptions are logged instead of terminating the process. A crash that does slip through now writes its FATAL line to the log before exiting.
- **Overlay froze while an app launched.** Actions used to run *before* the ring closed, on the render thread. The ring now closes first and shell launches run on a background thread, so the dial collapses instantly no matter how slow the app starts.
- **Middle-click in browsers was broken** while AeroDial ran with a middle-mouse trigger. New **tap-through**: a quick tap is passed to the app as a normal click; holding (or dragging) opens the dial. On by default for mouse triggers in Hold mode.
- Child rings, hover state and navigation now run on a single thread; the class of "child ring looks different depending on how you got there" bugs is gone at the root.

### Performance

- Active Apps is built only when a menu uses it, on a background thread, with exe icons extracted there too. Opening the dial no longer waits on `EnumWindows` and process lookups.
- The ring is rasterized only when something changes; idle frames just re-composite a cached layer (idle CPU drops from a constant ~125 fps redraw to a light ~24 fps tick).
- Gradient shaders are no longer churned during animations; theme colors are parsed once; audio device changes come from a WASAPI notification instead of re-creating COM objects every two seconds on the render thread.

### New

- **Open folder** action (Explorer, or select a file) and **Run command** action with Win+R semantics (`regedit`, `ms-settings:display`, `cmd /k dir`, `shell:startup`, `%APPDATA%`), including an optional **Run as administrator** switch.
- **System icon font**: icons are now Segoe Fluent Icons glyphs (Segoe MDL2 Assets on Windows 10) with a searchable picker of 120 named icons, and any glyph by hex code (`fluent:E8B7`). Existing configs are migrated automatically; exe and image icons are unchanged.
- **Keyboard navigation** while the dial is open: arrows move the highlight (child rings open as you go), 1-9 pick a slice, Enter runs, Backspace goes back, Escape closes. The keys never reach the app underneath.
- **Pause AeroDial** in the tray menu, and app profiles can now target **Disabled**, so the trigger passes straight through in games or apps that use it themselves.
- **Slices per ring** is now a 3 to 12 slider.
- **Export / Import** (Advanced page): menus, profiles, settings and custom themes in one file.
- **Auto (Windows accent)** theme that follows your desktop accent color.
- First-run tray hint, and an optional daily update check with a tray notice.
- Clipboard History shows an "Enable clipboard history" slice that opens the Windows setting when history is off, instead of an empty ring.

### Settings window

- Rebuilt as a **Windows 11 native** window: Mica backdrop, navigation rail with icons, and every color from the system theme, so it follows light/dark mode and your accent color.
- Eight pages instead of ten: scroll-wheel bindings moved into the slice editor, and Themes and Theme Editor merged into one page with a live preview (built-in themes can be duplicated and edited).
- Friendly action names ("Launch app", "Open folder", ...) with descriptions, and each action type comes with a sensible default icon.
- Presets moved into a dropdown; long labels no longer overflow.

### Under the hood

- Config files carry a schema version and are migrated on load; the previous file is kept as `config.json.bak`, and an unreadable file is set aside instead of overwritten.
- New `AeroDial.Core` library holds the models and pure logic (key-combo parsing, ring geometry, profile matching, migrations) with a unit test suite.
- `AeroDial.exe --selftest` drives the overlay and every settings page and saves screenshots, for release verification.

---

## AeroDial v2.0.0

A major update to AeroDial, the customisable radial launcher overlay for Windows. This release adds keyboard macros, per-app menu profiles, and a live now-playing display, alongside a ground-up redesign of the menu editor, a batch of visual fixes, and a rendering-performance pass.

---

### What's new in 2.0.0

**Macros — multi-step keyboard sequences**
- New **Macro** action type: chain typed text, key presses, decoupled key-down/key-up, and delays into a single slice
- Text is sent as Unicode, so it's keyboard-layout independent — e.g. type `FILLET` then press `Enter` reliably in AutoCAD and other command-line apps
- Key-down / key-up steps let you hold a key across later steps; if a macro is interrupted, any held keys are released so nothing gets stuck
- Built step-by-step in the menu editor

**App profiles — context-aware menus**
- Bind a menu to a specific app in **Settings → App Profiles**, with an "add from running app" picker
- When that app is in the foreground, opening the dial shows its menu; every other app falls back to the default

**Redesigned menu editor**
- The ring preview *is* the editor: click a `+` slot to add an item, click a slice to edit it, and **drag a slice** onto another slot to move or swap it
- Removing an item leaves an empty slot in place, so you control exactly where items and gaps sit on the ring
- Drill into submenus directly, with a breadcrumb to climb back out
- Working-copy model with an explicit **Save / Discard** bar, and a live WYSIWYG preview that matches the real overlay (gradients, glow, icons)

**Now playing + visualizer**
- Shows the currently-playing track title below the ring while media is playing, updating live when the track changes (from the dial or anywhere)
- Optional theme-coloured audio visualizer that pulses with the system volume
- Reads the Windows media session, so it works with any player that integrates with Windows — Spotify, Chrome / Edge / Firefox, Groove, VLC, and more
- Both are toggleable in **Settings → Appearance**

**Visual fixes**
- Fixed submenu (child) rings that rendered with inconsistent brightness / opacity depending on which slice opened them
- Softer, subtler slice borders on the **Midnight Teal, Ocean, Chalk,** and **Arctic** themes
- Custom app / exe icons now keep their natural colours on the light themes (**Chalk, Arctic**) instead of turning dark

**Under the hood**
- Reworked the overlay renderer to eliminate per-frame allocations — smoother animation, no GC hitches
- Action failures (missing app path, invalid URL, unrecognised key combo) now surface as a tray notification instead of failing silently
- Fixes: the tray icon tooltip now appears on hover; the Settings window reliably comes to the front when opened from the tray; global hook re-installation hardened against a rare crash

---

### Installation

1. Download `AeroDial.exe` below
2. Run it — AeroDial is a **single self-contained executable**
3. AeroDial starts silently in the system tray
4. Right-click the tray icon and choose **Settings** to configure your trigger and menus

> **No .NET runtime installation required** — the runtime is bundled inside the exe.

No installer. No extraction. No admin rights needed. No registry writes (other than the optional "start with Windows" toggle).

---

### Requirements

- Windows 10 version 2004 (build 19041) or later
- Windows 11 recommended for best visual results
- x64 architecture

---

### The download

A single self-contained `AeroDial.exe` (~110 MB). The .NET runtime, the WinUI 3 native libraries, and SkiaSharp are all bundled inside and self-extracted at runtime; the 11 built-in themes are compiled into the app. No extraction, no side files, and no separate runtime install.

---

### Known Limitations

- **Windows SmartScreen** may show a warning on first launch since the executable is not code-signed. Click **More info → Run anyway** to proceed. This is normal for unsigned indie software.
- **Ring scale changes** take effect the next time the menu is opened after saving, not instantly.
- **Clipboard History submenu** requires clipboard history to be enabled in Windows Settings (Settings > System > Clipboard). If it is off, the submenu will appear empty.
- **Now playing** only shows for media players that register a Windows media session — most modern players do (Spotify, browsers, VLC, Groove), but a few minimal players may not report a title.

---

### Feedback and Bug Reports

Found a bug or have a feature request? Please open an issue:
👉 https://github.com/mmatul06/AeroDial/issues

---

*Built with .NET 9 · WinUI 3 · SkiaSharp · C# — by Muhtasim Mahbub / 3M Design Solutions*

---
---

## AeroDial v1.0.0 -- Initial Release

First public release of AeroDial, a customisable radial launcher overlay for Windows. Press a trigger key or mouse button anywhere on screen and a circular menu opens at your cursor, letting you launch apps, fire key combos, control media, paste clipboard snippets, and navigate nested submenus instantly.

---

### Installation

1. Download `AeroDial-v1.0.0-win-x64.zip` below
2. Extract the zip to any folder (e.g. `C:\Apps\AeroDial\`)
3. Run `AeroDial.exe`
4. AeroDial starts silently in the system tray
5. Right-click the tray icon and choose **Settings** to configure your trigger and menus

> **No .NET runtime installation required** -- the runtime is bundled inside the zip.

No installer. No admin rights needed. No registry writes.

---

### Features

- **Radial launcher overlay** -- opens at your exact cursor position, works on top of any app including fullscreen games
- **4 / 6 / 8 slice ring** -- choose how many actions fit in each ring level
- **Nested submenus** -- hover a submenu slice to expand a child ring; center-click to go back
- **Three selection modes** -- Hover Dwell, Click, or Flick (cursor angle selects the aimed slice)
- **Hold or Toggle trigger** -- hold the trigger key to show/release-to-select, or toggle open/closed
- **Fully customisable trigger** -- any keyboard key, mouse button (including middle, X1, X2), or modifier combo
- **8 action types** -- Launch App, Open URL, Key Combo, Media, Run Script, Paste Clipboard, Open Submenu, Focus Window
- **Scroll wheel bindings** -- each slice can bind scroll-up/down to independent media actions
- **Active Tasks submenu** -- live list of open windows with per-app icons, rebuilt on every open
- **Clipboard History submenu** -- up to 8 recent clipboard text entries, ready to paste
- **11 built-in themes** -- Obsidian, Ember, Midnight Teal, Chalk, Neon, Cyberpunk, Ocean, Sunset, Matrix, Arctic, Sakura
- **Full custom theme support** -- JSON files in `%AppData%\AeroDial\themes\`
- **Theme Editor** -- create themes with 17 color fields and live preview, all inside Settings
- **40+ built-in icons** -- programmatic vector icons, white, tinted per-theme at render time
- **Exe icon extraction** -- Launch App items and Active Tasks show the app's own icon
- **Custom icons** -- any .png, .jpg, .ico, or .bmp file
- **Per-pixel transparency** -- DWM-composited overlay; no black box, works on any background
- **Multi-monitor support** -- overlay follows your cursor across any number of monitors at any DPI
- **Input blocking mode** -- optionally suppress non-trigger mouse clicks while the overlay is open
- **System tray only** -- no taskbar presence; right-click tray for Settings, About, or Quit
- **Autostart with Windows** -- optional, configurable in Settings

---

*Built with .NET 9 · WinUI 3 · SkiaSharp · C# -- by Muhtasim Mahbub / 3M Design Solutions*
