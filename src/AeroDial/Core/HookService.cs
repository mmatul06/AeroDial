// AeroDial — HookService.cs
// Low-level global keyboard and mouse hooks using raw Win32 SetWindowsHookEx.
// Runs the hook on a dedicated STA thread with its own message pump so it
// never blocks the UI thread or the render thread.
// This replaces H.Hooks entirely — more stable, zero third-party dependency.

using System.Runtime.InteropServices;
using AeroDial.Config;
using AeroDial.Core;

namespace AeroDial;

internal sealed class HookService : IDisposable
{
    // ── Events ────────────────────────────────────────────────────────────
    public event Action<System.Drawing.Point>? TriggerActivated;
    public event Action?                        TriggerReleased;
    /// <summary>Fires when the mouse wheel is scrolled while the overlay is open.
    /// Positive delta = scroll up, negative = scroll down.</summary>
    public event Action<int>?                   ScrollWheeled;

    // ── State ─────────────────────────────────────────────────────────────
    private Thread?       _hookThread;
    private nint          _mouseHook;
    private nint          _keyHook;
    private volatile uint _hookThreadId;   // Win32 thread ID — needed for PostThreadMessage
    private volatile bool _triggerHeld;
    private volatile bool _running;
    /// <summary>Set to true by OverlayController while the overlay window is visible.
    /// Used to gate scroll-wheel interception.</summary>
    public volatile bool  OverlayOpen;

    // Keep delegates alive — GC will collect them otherwise and crash.
    private LowLevelProc? _mouseProc;
    private LowLevelProc? _keyProc;

    // ── Win32 ─────────────────────────────────────────────────────────────
    private delegate nint LowLevelProc(int nCode, nint wParam, nint lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL    = 14;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_KEYUP       = 0x0101;
    private const int WM_SYSKEYDOWN  = 0x0104;
    private const int WM_SYSKEYUP    = 0x0105;
    private const int WM_MOUSEWHEEL  = 0x020A;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP   = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP   = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP   = 0x0208;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_XBUTTONUP   = 0x020C;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public Win32.POINT pt;
        public uint mouseData, flags, time;
        public nint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG msg, nint hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG msg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref MSG msg);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, nint wParam, nint lParam);

    private const uint WM_QUIT = 0x0012;
    // App-defined thread message used to marshal a hook reinstall onto the hook thread.
    private const uint WM_REINSTALL_HOOKS = 0x8000 + 1; // WM_APP + 1

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam, lParam;
        public uint time;
        public Win32.POINT pt;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public void Start()
    {
        if (_running) return;

        // Wait for any previously running hook thread to fully exit before
        // creating a new one. Without this, two threads can overlap and the
        // old thread's exit cleanup calls UnhookWindowsHookEx on the new
        // thread's hook handle, causing ExecutionEngineException.
        _hookThread?.Join(1000);

        _running = true;

        _hookThread = new Thread(HookThreadMain)
        {
            IsBackground = true,
            Name         = "AeroDial.HookThread",
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        App.Config.ConfigChanged += Reinstall;
        Logger.Info("HookService started.");
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        App.Config.ConfigChanged -= Reinstall;

        // Post WM_QUIT to unblock GetMessage on the hook thread so it can
        // exit cleanly. Without this it blocks indefinitely, accumulating
        // dead threads that still hold stale hook handles.
        uint tid = _hookThreadId;
        if (tid != 0) PostThreadMessage(tid, WM_QUIT, 0, 0);

        Logger.Info("HookService stopped.");
    }

    private void Reinstall()
    {
        // Low-level hooks must be installed and serviced on the thread that owns the
        // message pump. ConfigChanged fires on the UI thread, so marshal the actual
        // unhook/rehook onto the hook thread via a posted thread message instead of
        // calling Set/UnhookWindowsHookEx from the wrong thread.
        uint tid = _hookThreadId;
        if (tid != 0) PostThreadMessage(tid, WM_REINSTALL_HOOKS, 0, 0);
    }

    // Runs on the hook thread (dispatched from the message pump) so the new hooks
    // are owned by the correct thread and serviced by its GetMessage loop.
    private void DoReinstall()
    {
        if (_mouseHook != 0) { UnhookWindowsHookEx(_mouseHook); _mouseHook = 0; }
        if (_keyHook   != 0) { UnhookWindowsHookEx(_keyHook);   _keyHook   = 0; }
        InstallHooks();
    }

    // ── Hook thread ───────────────────────────────────────────────────────

    private void HookThreadMain()
    {
        _hookThreadId = GetCurrentThreadId();
        InstallHooks();

        // Win32 message pump — required to keep hooks alive.
        // GetMessage blocks until a message arrives; Stop() posts WM_QUIT
        // to unblock it so this thread exits promptly.
        while (_running && GetMessage(out var msg, 0, 0, 0) > 0)
        {
            if (msg.message == WM_REINSTALL_HOOKS)
            {
                DoReinstall();
                continue;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_mouseHook != 0) { UnhookWindowsHookEx(_mouseHook); _mouseHook = 0; }
        if (_keyHook   != 0) { UnhookWindowsHookEx(_keyHook);   _keyHook   = 0; }
        _hookThreadId = 0;
    }

    private void InstallHooks()
    {
        var cfg    = App.Config.Current.Trigger;
        var hMod   = GetModuleHandle(null);
        bool isMouse = cfg.VirtualKey is >= 0x01 and <= 0x06;

        // Always install the mouse hook: handles mouse-button trigger AND scroll-wheel
        // interception when the overlay is open (regardless of trigger type).
        _mouseProc = MouseHookCallback;
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
        if (_mouseHook == 0)
            Logger.Error($"Failed to install mouse hook. Error: {Marshal.GetLastWin32Error()}");

        // Install keyboard hook only when the trigger is a keyboard key.
        if (!isMouse)
        {
            _keyProc = KeyboardHookCallback;
            _keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, hMod, 0);
            if (_keyHook == 0)
                Logger.Error($"Failed to install keyboard hook. Error: {Marshal.GetLastWin32Error()}");
        }
    }

    // ── Mouse hook callback ───────────────────────────────────────────────

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var trigCfg  = App.Config.Current.Trigger;
            var info     = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int msg      = (int)wParam;
            bool isMouse = trigCfg.VirtualKey is >= 0x01 and <= 0x06;

            // ── Trigger detection (mouse-button triggers only) ───────────
            if (isMouse)
            {
                bool isDown = IsMouseDown(msg, trigCfg.VirtualKey, info.mouseData);
                bool isUp   = IsMouseUp(msg, trigCfg.VirtualKey, info.mouseData);

                if (isDown && !_triggerHeld && ModifiersMatch(trigCfg))
                {
                    _triggerHeld = true;
                    var pt = new System.Drawing.Point(info.pt.X, info.pt.Y);
                    Task.Run(() => TriggerActivated?.Invoke(pt));
                    return new nint(1); // always suppress — user chose this as trigger, never pass through
                }
                else if (isUp && _triggerHeld)
                {
                    _triggerHeld = false;
                    if (trigCfg.HoldMode)
                        Task.Run(() => TriggerReleased?.Invoke());
                    return new nint(1); // always suppress trigger-up too
                }
            }

            // ── Scroll wheel — captured when overlay is open ─────────────
            if (msg == WM_MOUSEWHEEL && OverlayOpen)
            {
                short delta = (short)((info.mouseData >> 16) & 0xFFFF);
                if (delta != 0) { int d = delta; Task.Run(() => ScrollWheeled?.Invoke(d)); }
                return new nint(1); // always suppress while overlay is open
            }

            // ── Input blocking — swallow non-trigger clicks when overlay is open ──
            if (OverlayOpen && App.Config.Current.Behavior.BlockInputWhenOpen)
            {
                if (msg is WM_LBUTTONDOWN or WM_LBUTTONUP
                        or WM_RBUTTONDOWN or WM_RBUTTONUP
                        or WM_MBUTTONDOWN or WM_MBUTTONUP
                        or WM_XBUTTONDOWN or WM_XBUTTONUP)
                {
                    // Don't block the trigger button itself (already handled above)
                    if (!IsMouseDown(msg, trigCfg.VirtualKey, info.mouseData) && !IsMouseUp(msg, trigCfg.VirtualKey, info.mouseData))
                        return new nint(1);
                }
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    // ── Keyboard hook callback ────────────────────────────────────────────

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var cfg  = App.Config.Current.Trigger;
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int msg  = (int)wParam;
            bool isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool isUp   = msg is WM_KEYUP   or WM_SYSKEYUP;

            if ((int)info.vkCode == cfg.VirtualKey)
            {
                if (isDown && !_triggerHeld && ModifiersMatch(cfg))
                {
                    _triggerHeld = true;
                    Win32.GetCursorPos(out var pt);
                    var point = new System.Drawing.Point(pt.X, pt.Y);
                    Task.Run(() => TriggerActivated?.Invoke(point));
                    return new nint(1); // suppress
                }
                else if (isUp && _triggerHeld)
                {
                    _triggerHeld = false;
                    if (cfg.HoldMode) Task.Run(() => TriggerReleased?.Invoke());
                    return new nint(1); // suppress
                }
            }
        }
        return CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // mouseData high word: 1 = XBUTTON1 (Mouse Button 4), 2 = XBUTTON2 (Mouse Button 5).
    // Both XButton events share WM_XBUTTONDOWN/UP — the high word is the only way to tell them apart.
    private static bool IsMouseDown(int msg, int vk, uint mouseData) => vk switch
    {
        0x01 => msg == WM_LBUTTONDOWN,
        0x02 => msg == WM_RBUTTONDOWN,
        0x04 => msg == WM_MBUTTONDOWN,
        0x05 => msg == WM_XBUTTONDOWN && (mouseData >> 16) == 1,
        0x06 => msg == WM_XBUTTONDOWN && (mouseData >> 16) == 2,
        _    => false,
    };

    private static bool IsMouseUp(int msg, int vk, uint mouseData) => vk switch
    {
        0x01 => msg == WM_LBUTTONUP,
        0x02 => msg == WM_RBUTTONUP,
        0x04 => msg == WM_MBUTTONUP,
        0x05 => msg == WM_XBUTTONUP && (mouseData >> 16) == 1,
        0x06 => msg == WM_XBUTTONUP && (mouseData >> 16) == 2,
        _    => false,
    };

    private static bool ModifiersMatch(TriggerConfig cfg)
    {
        bool ctrl  = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool alt   = (Win32.GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool shift = (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0;
        return cfg.RequireCtrl  == ctrl
            && cfg.RequireAlt   == alt
            && cfg.RequireShift == shift;
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose() => Stop();
}
