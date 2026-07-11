// AeroDial — TrayService.cs
// System tray icon using direct Win32 Shell_NotifyIcon.
// Uses GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS) to reliably
// obtain the module handle in self-contained .NET apps where GetModuleHandle(null)
// may return zero.

using System.Runtime.InteropServices;
using AeroDial.Core;
using AeroDial.UI.Views;
using Microsoft.UI.Dispatching;

namespace AeroDial;

internal sealed class TrayService : IDisposable
{
    public DispatcherQueue DispatcherQueue { get; }

    private System.Threading.Thread? _trayThread;
    private TrayWindow?               _trayWindow;

    public TrayService()
    {
        DispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "TrayService must be created on the UI thread.");
    }

    public void Initialize()
    {
        _trayThread = new System.Threading.Thread(() =>
        {
            _trayWindow = new TrayWindow(DispatcherQueue);
            _trayWindow.RunMessageLoop();
        })
        {
            IsBackground = true,
            Name         = "AeroDial.TrayThread",
        };
        _trayThread.SetApartmentState(System.Threading.ApartmentState.STA);
        _trayThread.Start();
        Logger.Info("TrayService initialized.");
    }

    /// <summary>Show a tray balloon notification (used to surface action failures to the user).</summary>
    public void ShowBalloon(string title, string message)
        => _trayWindow?.ShowBalloon(title, message);

    public void Dispose()
    {
        _trayWindow?.Destroy();
        Logger.Info("TrayService disposed.");
    }
}

// ── Tray window ───────────────────────────────────────────────────────────────

internal sealed class TrayWindow
{
    // ── Constants ─────────────────────────────────────────────────────────
    private const uint WM_USER_TRAY   = 0x0401;
    private const uint NIM_ADD        = 0;
    private const uint NIM_MODIFY     = 1;
    private const uint NIM_DELETE     = 2;
    private const uint NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE    = 1;
    private const uint NIF_ICON       = 2;
    private const uint NIF_TIP        = 4;
    private const uint NIF_SHOWTIP    = 0x80;
    private const uint NIF_INFO       = 0x10;
    private const uint NIIF_WARNING   = 0x02;
    private const uint NOTIFYICON_VERSION_4 = 4;
    private const int  WM_DESTROY     = 0x0002;
    private const int  WM_COMMAND     = 0x0111;
    private const int  WM_RBUTTONUP   = 0x0205;
    private const int  WM_LBUTTONDBLCLK = 0x0203;
    private const int  IDM_SETTINGS   = 1001;
    private const int  IDM_ABOUT      = 1002;
    private const int  IDM_QUIT       = 1003;
    private const uint GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;
    private const uint GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT = 0x00000002;

    private nint                 _hwnd;
    private nint                 _hIcon;
    private readonly DispatcherQueue _dispatcher;
    private WndProcDelegate?     _wndProcDelegate;

    public TrayWindow(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void RunMessageLoop()
    {
        var hInstance = GetAppModuleHandle();
        Logger.Info($"TrayWindow hInstance: 0x{hInstance:X}");

        RegisterWindowClass(hInstance);
        CreateMessageWindow(hInstance);

        if (_hwnd == 0)
        {
            Logger.Error($"TrayWindow: CreateWindow failed, error {Marshal.GetLastWin32Error()}");
            return;
        }

        AddTrayIcon();
        Logger.Info("Tray icon added.");

        while (GetMessage(out var msg, 0, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    public void Destroy()
    {
        if (_hwnd != 0) PostMessage(_hwnd, 0x0012 /* WM_QUIT */, 0, 0);
    }

    // ── Module handle ─────────────────────────────────────────────────────
    // GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | UNCHANGED_REFCOUNT:
    // retrieves the handle of the module containing the specified address —
    // works correctly in self-contained .NET apps where the main module
    // handle may not be what GetModuleHandle(null) returns.

    private static nint GetAppModuleHandle()
    {
        // Try GetModuleHandleEx with the address of a known function first
        GetModuleHandleEx(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
            GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            typeof(TrayWindow).TypeHandle.Value,
            out nint h);

        if (h != 0) return h;

        // Fallback to classic approach
        GetModuleHandleEx(0, null, out h);
        return h;
    }

    // ── Window class + window ─────────────────────────────────────────────

    private void RegisterWindowClass(nint hInstance)
    {
        _wndProcDelegate = WndProc;
        var wc = new WNDCLASSEX
        {
            cbSize        = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance     = hInstance,
            lpszClassName = "AeroDialTray_v1",
        };
        ushort atom = RegisterClassEx(ref wc);
        if (atom == 0)
            Logger.Warn($"RegisterClassEx failed or class already registered: {Marshal.GetLastWin32Error()}");
    }

    private void CreateMessageWindow(nint hInstance)
    {
        _hwnd = CreateWindowEx(
            0,
            "AeroDialTray_v1",
            "AeroDial Tray",
            0, 0, 0, 0, 0,
            new nint(-3), // HWND_MESSAGE — message-only window
            0,
            hInstance,
            0);
    }

    // ── Tray icon ─────────────────────────────────────────────────────────

    private void AddTrayIcon()
    {
        _hIcon = LoadTrayIcon();
        var nid = MakeNID();
        Shell_NotifyIcon(NIM_ADD, ref nid);
        nid.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref nid);
    }

    private void RemoveTrayIcon()
    {
        var nid = MakeNID();
        Shell_NotifyIcon(NIM_DELETE, ref nid);
        if (_hIcon != 0) DestroyIcon(_hIcon);
    }

    // Balloon notification. Identified by hWnd+uID, so this is safe to call from
    // any thread — the shell delivers it and routes clicks back to _hwnd's WndProc.
    public void ShowBalloon(string title, string message)
    {
        if (_hwnd == 0) return;
        var nid = MakeNID();
        nid.uFlags      = NIF_INFO;
        nid.szInfoTitle = title.Length   > 63  ? title[..63]    : title;
        nid.szInfo      = message.Length > 255 ? message[..255] : message;
        nid.dwInfoFlags = NIIF_WARNING;
        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    private NOTIFYICONDATA MakeNID() => new()
    {
        cbSize           = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd             = _hwnd,
        uID              = 1,
        // NIF_SHOWTIP: with NOTIFYICON_VERSION_4 the standard tooltip is suppressed
        // unless this flag is set — without it, hovering the icon shows nothing.
        uFlags           = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
        uCallbackMessage = WM_USER_TRAY,
        hIcon            = _hIcon,
        szTip            = AppConstants.AppName,
        szInfo           = "",
        szInfoTitle      = "",
    };

    // ── Message pump ──────────────────────────────────────────────────────

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_USER_TRAY)
        {
            uint note = (uint)(lParam & 0xFFFF);
            if (note == (uint)WM_RBUTTONUP)
                ShowContextMenu();
            else if (note == (uint)WM_LBUTTONDBLCLK)
                _dispatcher.TryEnqueue(() => SettingsWindow.ShowOrActivate());
            return 0;
        }

        if (msg == (uint)WM_COMMAND)
        {
            switch ((int)(wParam & 0xFFFF))
            {
                case IDM_SETTINGS:
                    _dispatcher.TryEnqueue(() => SettingsWindow.ShowOrActivate());
                    break;
                case IDM_ABOUT:
                    _dispatcher.TryEnqueue(() => AboutDialog.ShowOrActivate());
                    break;
                case IDM_QUIT:
                    RemoveTrayIcon();
                    _dispatcher.TryEnqueue(() => App.RequestShutdown());
                    break;
            }
            return 0;
        }

        if (msg == (uint)WM_DESTROY)
        {
            RemoveTrayIcon();
            PostQuitMessage(0);
            return 0;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        nint hMenu = CreatePopupMenu();
        InsertMenu(hMenu, 0, 0x0400,              (nint)IDM_SETTINGS, "Settings");
        InsertMenu(hMenu, 1, 0x0400 | 0x0800, 0,                      null);
        InsertMenu(hMenu, 2, 0x0400,              (nint)IDM_ABOUT,    "About AeroDial");
        InsertMenu(hMenu, 3, 0x0400 | 0x0800, 0,                      null);
        InsertMenu(hMenu, 4, 0x0400,              (nint)IDM_QUIT,     "Quit AeroDial");

        SetForegroundWindow(_hwnd);
        GetCursorPos(out var pt);
        TrackPopupMenu(hMenu, 0x0002 /* TPM_RIGHTBUTTON */,
            pt.X, pt.Y, 0, _hwnd, 0);
        DestroyMenu(hMenu);
    }

    private static nint LoadTrayIcon()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets", "aerodial.ico");

        if (File.Exists(path))
        {
            nint icon = LoadImage(0, path, 1 /* IMAGE_ICON */, 32, 32,
                0x0010 /* LR_LOADFROMFILE */);
            if (icon != 0) return icon;
        }

        // Fallback: application default icon
        return LoadIcon(0, new nint(32512) /* IDI_APPLICATION */);
    }

    // ── Structs ───────────────────────────────────────────────────────────

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint   cbSize, style;
        public nint   lpfnWndProc;
        public int    cbClsExtra, cbWndExtra;
        public nint   hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string? lpszClassName;
        public nint   hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint   cbSize;
        public nint   hWnd;
        public uint   uID, uFlags, uCallbackMessage;
        public nint   hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint   dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint   uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint   dwInfoFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd; public uint message;
        public nint wParam, lParam; public uint time; public POINT pt;
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetModuleHandleEx(uint flags, nint address, out nint handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetModuleHandleEx(uint flags, string? name, out nint handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern ushort RegisterClassEx(ref WNDCLASSEX c);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint CreateWindowEx(uint ex, string cls, string wnd, uint style,
        int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);

    [DllImport("user32.dll")] static extern nint DefWindowProc(nint h, uint m, nint w, nint l);
    [DllImport("user32.dll")] static extern int  GetMessage(out MSG m, nint h, uint a, uint b);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] static extern nint DispatchMessageW(ref MSG m);
    [DllImport("user32.dll")] static extern bool PostMessage(nint h, uint m, nint w, nint l);
    [DllImport("user32.dll")] static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint h);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool InsertMenu(nint h, uint pos, uint flags, nint id, string? text);
    [DllImport("user32.dll")] static extern bool TrackPopupMenu(
        nint h, uint flags, int x, int y, int r, nint hwnd, nint rect);
    [DllImport("user32.dll")] static extern bool DestroyMenu(nint h);
    [DllImport("user32.dll")] static extern bool DestroyIcon(nint h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern nint LoadImage(nint inst, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern nint LoadIcon(nint inst, nint name);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA d);
}
