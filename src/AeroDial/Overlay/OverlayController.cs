// AeroDial — OverlayController.cs
// Owns the overlay window lifecycle and menu navigation state machine.
// Submenus open automatically when you hover over a SubMenu slice.

using System.Diagnostics;
using System.Text;
using AeroDial.Config;
using AeroDial.Core;

namespace AeroDial.Overlay;

internal sealed class OverlayController : IDisposable
{
    private OverlayWindow?                   _window;
    private readonly Stack<RadialMenuConfig> _menuStack    = new();
    private RadialMenuConfig?                _currentMenu;
    private int                              _hoveredIndex      = -1;
    private bool                             _isOpen;

    // Child ring state — tracks which L2 submenu is expanded as the outer ring
    private RadialMenuConfig? _childMenu;
    private int               _childHoveredIndex = -1;
    private int               _childParentIndex  = -1; // L1 slice index that opened the L2 ring

    // L3 ring state — shown when an L2 SubMenu item is hovered
    private RadialMenuConfig? _l3Menu;
    private int               _l3HoveredIndex = -1;
    private int               _l3ParentIndex  = -1; // L2 item index that opened the L3 ring

    // Dynamic menu caches — rebuilt each time the overlay opens
    private volatile RadialMenuConfig? _activeTasksMenu;
    private volatile RadialMenuConfig? _clipboardMenu;

    // Active Tasks is only built when some menu actually references it, and always on a
    // background thread: EnumWindows + Process.MainModule per window used to run
    // synchronously between the trigger and the first pixel.
    private bool _needsActiveTasks;
    private int  _activeTasksBuildGen;

    // pid -> (process name, exe path). MainModule is slow and throws for elevated
    // processes; resolve each process once per session.
    private static readonly Dictionary<uint, (string Name, string Icon)> s_exeIconByPid = new();

    // HWND of the window that had focus when the overlay opened.
    // Restored on close so games and fullscreen apps recapture their mouse cursor.
    private nint _prevForegroundHwnd;

    // All state above is owned by the UI thread: the renderer posts input events to it
    // (see OverlayRenderer.Post) and HookService callbacks marshal via DispatcherQueue.

    public OverlayController()
    {
        App.Hooks.TriggerActivated += OnTriggerActivated;
        App.Hooks.TriggerReleased  += OnTriggerReleased;
        App.Hooks.ScrollWheeled    += OnScrollWheeled;
        App.Config.ConfigChanged   += RefreshMenuFlags;
        RefreshMenuFlags();
    }

    private void RefreshMenuFlags()
    {
        _needsActiveTasks = App.Config.Current.Menus.Any(m =>
            m.Items.Any(i => i.SubMenuId == AppConstants.ActiveTasksMenuId));
    }

    // ── Trigger ───────────────────────────────────────────────────────────

    private void OnTriggerActivated(System.Drawing.Point cursorPos)
    {
        App.Tray.DispatcherQueue.TryEnqueue(() =>
        {
            if (_isOpen && !App.Config.Current.Trigger.HoldMode)
            {
                // Flick mode: second press executes the aimed slice instead of just closing
                if (App.Config.Current.Behavior.SelectionMode == SelectionMode.Flick
                    && _hoveredIndex >= 0
                    && _currentMenu is not null
                    && _hoveredIndex < _currentMenu.Items.Count)
                {
                    ExecuteItem(_currentMenu.Items[_hoveredIndex]);
                }
                else
                {
                    Close();
                }
                return;
            }
            Open(cursorPos);
        });
    }

    private void OnTriggerReleased()
    {
        App.Tray.DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isOpen) return;

            var  bCfg     = App.Config.Current.Behavior;
            bool holdMode = App.Config.Current.Trigger.HoldMode;
            bool onRelease = bCfg.SelectionMode == SelectionMode.Flick || bCfg.LaunchOnRelease;

            // Pick the item to run: L3 > L2 > L1. Submenu slices never execute on release.
            MenuItemConfig? toRun = null;
            if (onRelease)
            {
                if (_l3HoveredIndex >= 0 && _l3Menu is not null && _l3HoveredIndex < _l3Menu.Items.Count)
                    toRun = _l3Menu.Items[_l3HoveredIndex];
                else if (_childHoveredIndex >= 0 && _childMenu is not null && _childHoveredIndex < _childMenu.Items.Count)
                    toRun = _childMenu.Items[_childHoveredIndex];
                else if (_hoveredIndex >= 0 && _currentMenu is not null && _hoveredIndex < _currentMenu.Items.Count)
                    toRun = _currentMenu.Items[_hoveredIndex];

                if (toRun is not null && toRun.ActionType == ActionType.SubMenu) toRun = null;
            }

            // Close first, then run: the ring collapses immediately and the previous
            // foreground window is restored before any keystrokes or launches happen.
            if (holdMode) Close();
            if (toRun is not null) ExecuteItem(toRun);
        });
    }

    // ── Open / Close ──────────────────────────────────────────────────────

    private void Open(System.Drawing.Point cursorPos)
    {
        // Capture the current foreground window so we can restore it on close.
        // This allows games and fullscreen apps to recapture their mouse cursor
        // after the overlay is dismissed (they need a SetForegroundWindow signal
        // to know they've regained focus and should re-engage mouse capture).
        _prevForegroundHwnd = Win32.GetForegroundWindow();

        // Rebuild dynamic menus in the background. Nothing here may delay the first frame.
        if (_needsActiveTasks) StartActiveTasksBuild();
        _ = BuildClipboardHistoryMenuAsync().ContinueWith(t =>
        {
            if (!t.IsFaulted) _clipboardMenu = t.Result;
            else Logger.Warn("BuildClipboardHistoryMenuAsync failed", t.Exception);
        }, TaskContinuationOptions.ExecuteSynchronously);

        string? processName = GetForegroundProcessName();
        var menu = App.Config.GetActiveMenu(processName);
        if (menu is null)
        {
            Logger.Debug($"Overlay not opened: dial is disabled for '{processName}'.");
            return;
        }
        _currentMenu = menu;
        PrefetchSubmenuIcons(_currentMenu);
        _menuStack.Clear();
        _hoveredIndex      = -1;
        _childMenu         = null;
        _childHoveredIndex = -1;
        _childParentIndex  = -1;
        _l3Menu            = null;
        _l3HoveredIndex    = -1;
        _l3ParentIndex     = -1;
        _isOpen            = true;
        App.Hooks.OverlayOpen = true;

        if (_window is null)
        {
            _window = new OverlayWindow(this);
            _window.HoveredIndexChanged      += OnHoveredIndexChanged;
            _window.ItemClicked              += OnItemClicked;
            _window.CenterClicked            += NavigateBack;
            _window.ChildItemClicked         += OnChildItemClicked;
            _window.ChildHoveredIndexChanged += OnChildHoveredIndexChanged;
            _window.L3ItemClicked            += OnL3ItemClicked;
            _window.L3HoveredIndexChanged    += idx => _l3HoveredIndex = idx;
            _window.ClickedOutside           += Close;
        }

        _window.Show(cursorPos, _currentMenu);
        Logger.Info($"Overlay opened — '{_currentMenu.Name}'");
    }

    /// <summary>Called from Program.ActivationCallback when a second instance signals activation.</summary>
    public void OpenAtCursor(System.Drawing.Point cursorPos)
    {
        if (_isOpen) return;
        Open(cursorPos);
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen            = false;
        _childMenu         = null;
        _childHoveredIndex = -1;
        _childParentIndex  = -1;
        _l3Menu            = null;
        _l3HoveredIndex    = -1;
        _l3ParentIndex     = -1;
        App.Hooks.OverlayOpen = false;
        _menuStack.Clear();
        _window?.Hide();

        // Restore focus to the window that was active when the overlay opened.
        // This signals to games / fullscreen apps that they have focus again so
        // they can re-engage their mouse capture or ClipCursor restriction.
        if (_prevForegroundHwnd != 0)
            Win32.SetForegroundWindow(_prevForegroundHwnd);

        Logger.Info("Overlay closed.");
    }

    // ── Hover — show/hide outer child ring for SubMenu items ─────────────

    private void OnHoveredIndexChanged(int index)
    {
        if (!_isOpen) return; // stale event posted before Close()
        _hoveredIndex = index;

        if (index < 0 || _currentMenu is null || index >= _currentMenu.Items.Count)
        {
            // Cursor is in a gap, dead zone, or center — do NOT collapse the child ring.
            // The cursor is transiting between slices; collapsing here causes the child
            // ring to flash every time the user sweeps across a gap.
            return;
        }

        var item = _currentMenu.Items[index];

        if (item.ActionType == ActionType.SubMenu && item.SubMenuId is not null)
        {
            // Switch the child ring to this submenu. Re-sync also when the parent slice
            // changes (even if it resolves to the same submenu object) — otherwise stale
            // _childParentIndex / L3 / ring-thinning state carries over and the child ring
            // renders differently depending on which parent opened it.
            var sub = ResolveSubMenu(item.SubMenuId);
            if (sub is not null && (sub != _childMenu || index != _childParentIndex))
            {
                _childMenu         = sub;
                _childParentIndex  = index;
                _childHoveredIndex = -1;
                // Switching L2 resets L3
                _l3Menu         = null;
                _l3ParentIndex  = -1;
                _l3HoveredIndex = -1;
                _window?.HideL3Menu();
                _window?.ShowChildMenu(sub, index);
            }
        }
        else if (_childMenu != null)
        {
            // Non-SubMenu slice hovered — dismiss the child ring (and L3 if open).
            // The cursor landed on a real, non-submenu slice, so the child ring is no longer relevant.
            _childMenu         = null;
            _childParentIndex  = -1;
            _childHoveredIndex = -1;
            _l3Menu            = null;
            _l3ParentIndex     = -1;
            _l3HoveredIndex    = -1;
            _window?.HideL3Menu();
            _window?.HideChildMenu();
        }
    }

    // ── Click ─────────────────────────────────────────────────────────────

    private void OnItemClicked(int index)
    {
        if (!_isOpen || _currentMenu is null || index < 0 || index >= _currentMenu.Items.Count) return;
        var item = _currentMenu.Items[index];

        if (item.ActionType == ActionType.SubMenu)
        {
            // Clicking the same slice that opened the child ring toggles it closed.
            // Clicking a different submenu slice is handled entirely by hover — no action needed here.
            if (_childMenu != null && index == _childParentIndex)
            {
                _childMenu         = null;
                _childParentIndex  = -1;
                _childHoveredIndex = -1;
                _window?.HideChildMenu();
            }
            return;
        }

        ExecuteItem(item);
    }

    private void OnChildHoveredIndexChanged(int idx)
    {
        if (!_isOpen) return;
        _childHoveredIndex = idx;

        if (idx < 0 || _childMenu is null || idx >= _childMenu.Items.Count)
            return; // cursor in gap — do NOT collapse L3 (same philosophy as L2 hover)

        var item = _childMenu.Items[idx];
        if (item.ActionType == ActionType.SubMenu && item.SubMenuId is not null)
        {
            // Auto-open L3 (same pattern as L1 hover → L2 auto-open)
            var l3 = ResolveSubMenu(item.SubMenuId);
            if (l3 is not null && (l3 != _l3Menu || idx != _l3ParentIndex))
            {
                _l3Menu         = l3;
                _l3ParentIndex  = idx;
                _l3HoveredIndex = -1;
                _window?.ShowL3Menu(l3, idx);
            }
        }
        else if (_l3Menu != null)
        {
            // Non-SubMenu L2 item hovered — dismiss the L3 ring.
            _l3Menu         = null;
            _l3ParentIndex  = -1;
            _l3HoveredIndex = -1;
            _window?.HideL3Menu();
        }
    }

    private void OnChildItemClicked(int index)
    {
        if (!_isOpen || _childMenu is null || index < 0 || index >= _childMenu.Items.Count) return;
        ExecuteItem(_childMenu.Items[index]);
    }

    private void OnL3ItemClicked(int index)
    {
        if (!_isOpen || _l3Menu is null || index < 0 || index >= _l3Menu.Items.Count) return;
        ExecuteItem(_l3Menu.Items[index]);
    }

    private void ExecuteItem(MenuItemConfig item)
    {
        if (item.ActionType == ActionType.SubMenu && item.SubMenuId is not null)
        {
            if (!_isOpen) return;
            var sub = ResolveSubMenu(item.SubMenuId);
            if (sub is not null)
            {
                _menuStack.Push(_currentMenu!);
                _currentMenu  = sub;
                _hoveredIndex = -1;
                _window?.NavigateTo(sub, hasParent: true);
                return;
            }
        }

        // Close BEFORE executing. Close() hides the ring and restores the previous
        // foreground window synchronously; the action then runs (shell launches go
        // to a threadpool thread inside the dispatcher) without holding the ring open.
        if (App.Config.Current.Behavior.CloseOnActionExecuted)
            Close();

        App.Dispatcher.Execute(item);
    }

    // ── Scroll wheel ──────────────────────────────────────────────────────

    private void OnScrollWheeled(int delta)
    {
        // Already on a background thread from HookService. Must marshal to UI thread
        // because ExecuteMedia (via ActionDispatcher) may touch UI state.
        App.Tray.DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isOpen || _hoveredIndex < 0 || _currentMenu is null) return;
            if (_hoveredIndex >= _currentMenu.Items.Count) return;

            var item   = _currentMenu.Items[_hoveredIndex];
            var action = delta > 0 ? item.ScrollUpAction : item.ScrollDownAction;
            if (action is null) return;

            // Execute the media action without closing the menu
            App.Dispatcher.Execute(new AeroDial.Config.MenuItemConfig
            {
                Label       = "Scroll",
                ActionType  = AeroDial.Config.ActionType.Media,
                MediaAction = action,
            });

            // Flash a ring around the overlay so the user gets visual feedback
            _window?.TriggerVolumeFlash();
        });
    }

    // ── Navigate back (center click) ──────────────────────────────────────

    public void NavigateBack()
    {
        if (!_isOpen) return;
        // Dismiss L3 first if open
        if (_l3Menu != null)
        {
            _l3Menu         = null;
            _l3HoveredIndex = -1;
            _l3ParentIndex  = -1;
            _window?.HideL3Menu();
            return;
        }
        // Then dismiss L2
        if (_childMenu != null)
        {
            _childMenu         = null;
            _childHoveredIndex = -1;
            _childParentIndex  = -1;
            _window?.HideChildMenu();
            return;
        }
        if (_menuStack.Count == 0) { Close(); return; }
        _currentMenu  = _menuStack.Pop();
        _hoveredIndex = -1;
        _window?.NavigateTo(_currentMenu, hasParent: _menuStack.Count > 0);
    }

    // ── Dynamic menus ─────────────────────────────────────────────────────

    /// <summary>Resolves a subMenuId, handling magic dynamic IDs.</summary>
    private RadialMenuConfig? ResolveSubMenu(string subMenuId)
    {
        if (subMenuId == AppConstants.ActiveTasksMenuId)
            return _activeTasksMenu
                ?? new RadialMenuConfig { Id = subMenuId, Name = "Active Apps", Items = [] };

        if (subMenuId == AppConstants.ClipboardHistoryMenuId)
            return _clipboardMenu
                ?? new RadialMenuConfig { Id = subMenuId, Name = "Clipboard History", Items = [] };

        return App.Config.GetMenu(subMenuId);
    }

    /// <summary>Builds the Active Apps menu on a threadpool thread, extracts its exe icons
    /// there too, then swaps it into any ring currently showing the list.</summary>
    private void StartActiveTasksBuild()
    {
        int gen = Interlocked.Increment(ref _activeTasksBuildGen);
        _ = Task.Run(() =>
        {
            RadialMenuConfig menu;
            try { menu = BuildActiveTasksMenu(); }
            catch (Exception ex) { Logger.Warn("BuildActiveTasksMenu failed", ex); return; }

            // Shell icon extraction happens here, never inside a render frame.
            IconRegistry.Prefetch(menu.Items.Select(i => i.Icon), App.Themes.ActiveTheme.IconStrokeScale);

            if (gen != _activeTasksBuildGen) return; // a newer open superseded this build
            _activeTasksMenu = menu;
            App.Tray.DispatcherQueue.TryEnqueue(() => OnActiveTasksReady(menu));
        });
    }

    // UI thread. If the user already hovered into Active Apps while it was building, the
    // ring is showing the previous list (or an empty placeholder): swap in the fresh one in
    // place, without restarting the pop-out animation.
    private void OnActiveTasksReady(RadialMenuConfig menu)
    {
        if (!_isOpen) return;
        if (_childMenu?.Id == AppConstants.ActiveTasksMenuId)
        {
            _childMenu         = menu;
            _childHoveredIndex = -1;
            _l3Menu = null; _l3ParentIndex = -1; _l3HoveredIndex = -1;
            _window?.HideL3Menu();
            _window?.ReplaceChildMenu(menu);
        }
        else if (_currentMenu?.Id == AppConstants.ActiveTasksMenuId)
        {
            _currentMenu  = menu;
            _hoveredIndex = -1;
            _window?.ReplaceMenu(menu);
        }
    }

    /// <summary>Warms the icon cache for every static submenu the current menu can open,
    /// so the first hover into a child ring does not stall on icon decoding.</summary>
    private static void PrefetchSubmenuIcons(RadialMenuConfig menu)
    {
        var keys = new List<string?>();
        foreach (var item in menu.Items)
        {
            if (item.ActionType != ActionType.SubMenu || item.SubMenuId is null) continue;
            var sub = App.Config.GetMenu(item.SubMenuId);
            if (sub is null) continue;
            foreach (var child in sub.Items) keys.Add(child.Icon);
        }
        if (keys.Count == 0) return;
        float strokeScale = App.Themes.ActiveTheme.IconStrokeScale;
        _ = Task.Run(() => IconRegistry.Prefetch(keys, strokeScale));
    }

    private static RadialMenuConfig BuildActiveTasksMenu()
    {
        // Pass 1: enumerate cheaply (no process lookups) and cap the list first.
        // EnumWindows returns windows front-to-back (most-recent first).
        // Cap at 16 so slices stay wide enough to read even on a full circle.
        const int MaxActiveTasks = 16;
        var windows = new List<(nint Hwnd, string Title)>();

        Win32.EnumWindows((hwnd, _) =>
        {
            try
            {
                if (windows.Count >= MaxActiveTasks) return false; // stop enumerating
                if (!Win32.IsWindowVisible(hwnd)) return true;

                // Skip cloaked windows (e.g. UWP apps backgrounded by the shell)
                Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_CLOAKED, out int cloaked, sizeof(int));
                if (cloaked != 0) return true;

                var sb    = new StringBuilder(256);
                int chars = Win32.GetWindowTextW(hwnd, sb, sb.Capacity);
                if (chars == 0) return true;

                var title = sb.ToString().Trim();
                if (string.IsNullOrEmpty(title)) return true;

                // Skip the Windows shell desktop window — always titled "Program Manager"
                if (title == "Program Manager") return true;

                // Skip tool windows not explicitly shown in the taskbar.
                // A window with WS_EX_TOOLWINDOW but without WS_EX_APPWINDOW is not taskbar-visible.
                // This filters out overlays like Nvidia GeForce Experience that have titles but
                // are inaccessible to the user (they're not in the taskbar, not activatable).
                uint exStyle  = (uint)Win32.GetWindowLongPtrW(hwnd, Win32.GWL_EXSTYLE);
                bool toolWin  = (exStyle & Win32.WS_EX_TOOLWINDOW) != 0;
                bool appWin   = (exStyle & Win32.WS_EX_APPWINDOW)  != 0;
                if (toolWin && !appWin) return true;

                windows.Add((hwnd, title));
            }
            catch (Exception ex)
            {
                Logger.Debug($"BuildActiveTasksMenu: skipped hwnd 0x{hwnd:X} — {ex.Message}");
            }

            return true; // continue enumeration
        }, 0);

        // Pass 2: resolve per-app icons for the capped list only.
        var items = new List<MenuItemConfig>(windows.Count);
        foreach (var (hwnd, title) in windows)
        {
            items.Add(new MenuItemConfig
            {
                Label        = title.Length > 30 ? string.Concat(title.AsSpan(0, 30), "…") : title,
                Icon         = ResolveExeIcon(hwnd),
                ActionType   = ActionType.FocusWindow,
                WindowHandle = hwnd,
            });
        }

        return new RadialMenuConfig
        {
            Id    = AppConstants.ActiveTasksMenuId,
            Name  = "Active Apps",
            Items = items,
        };
    }

    /// <summary>Exe path for the window's process (used as its icon key), or "apps" for
    /// elevated / protected processes. Cached per pid for the session, validated by name.</summary>
    private static string ResolveExeIcon(nint hwnd)
    {
        try
        {
            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            using var proc = Process.GetProcessById((int)pid);
            string name = proc.ProcessName; // cheap; works for elevated processes too

            lock (s_exeIconByPid)
                if (s_exeIconByPid.TryGetValue(pid, out var cached) && cached.Name == name)
                    return cached.Icon;

            string icon = "apps";
            try
            {
                var exePath = proc.MainModule?.FileName; // slow; throws for elevated processes
                if (!string.IsNullOrEmpty(exePath)) icon = exePath;
            }
            catch { /* protected / elevated process — fallback icon, and remember that */ }

            lock (s_exeIconByPid) s_exeIconByPid[pid] = (name, icon);
            return icon;
        }
        catch { return "apps"; }
    }

    private static async Task<RadialMenuConfig> BuildClipboardHistoryMenuAsync()
    {
        var items = new List<MenuItemConfig>();

        try
        {
            var result = await Windows.ApplicationModel.DataTransfer.Clipboard
                .GetHistoryItemsAsync();

            if (result.Status ==
                Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.Success)
            {
                foreach (var entry in result.Items.Take(8))
                {
                    if (!entry.Content.Contains(
                            Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                        continue;

                    var text = await entry.Content.GetTextAsync();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var preview = text.Trim().Replace('\r', ' ').Replace('\n', ' ');
                    items.Add(new MenuItemConfig
                    {
                        Label      = preview.Length > 28 ? string.Concat(preview.AsSpan(0, 28), "…") : preview,
                        Icon       = "default",
                        ActionType = ActionType.PasteClipboard,
                        ClipText   = text,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not read clipboard history", ex);
        }

        return new RadialMenuConfig
        {
            Id    = AppConstants.ClipboardHistoryMenuId,
            Name  = "Clipboard History",
            Items = items,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string? GetForegroundProcessName()
    {
        try
        {
            var hwnd = Win32.GetForegroundWindow();
            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch { return null; }
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        App.Hooks.TriggerActivated -= OnTriggerActivated;
        App.Hooks.TriggerReleased  -= OnTriggerReleased;
        App.Hooks.ScrollWheeled    -= OnScrollWheeled;
        App.Config.ConfigChanged   -= RefreshMenuFlags;
        _window?.Dispose();
    }
}
