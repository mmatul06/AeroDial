// AeroDial — Win32.cs
// All P/Invoke declarations live here. Centralising them keeps the rest of the
// codebase free of [DllImport] noise and makes it easy to audit what native
// APIs the app touches.

using System.Runtime.InteropServices;

namespace AeroDial.Core;

internal static partial class Win32
{
    // ── Structs ───────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT  { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint  cbSize;
        public RECT  rcMonitor;
        public RECT  rcWork;
        public uint  dwFlags;
    }

    // ── Cursor / input ────────────────────────────────────────────────────

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);

    // ── Window management ─────────────────────────────────────────────────

    [LibraryImport("user32.dll")]
    public static partial nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll")]
    public static partial nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter,
        int X, int Y, int cx, int cy,
        uint uFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(
        nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    // ── Monitor info ──────────────────────────────────────────────────────

    public delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, ref RECT lprcMonitor, nint dwData);

    [LibraryImport("user32.dll", EntryPoint = "EnumDisplayMonitors")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayMonitors(
        nint hdc, nint lprcClip,
        MonitorEnumProc lpfnEnum, nint dwData);

    [LibraryImport("user32.dll", EntryPoint = "MonitorFromPoint")]
    public static partial nint MonitorFromPoint(POINT pt, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(
        nint hMonitor, uint dpiType, out uint dpiX, out uint dpiY);

    // ── DWM ───────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight;
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint hwnd, uint dwAttribute, out int pvAttribute, int cbAttribute);

    public const uint DWMWA_CLOAKED = 14;

    // ── Window enumeration ────────────────────────────────────────────────

    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextW(nint hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    // ── Shell icon extraction ─────────────────────────────────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int ExtractIconEx(
        string lpszFile, int nIconIndex,
        nint[]? phiconLarge, nint[]? phiconSmall, uint nIcons);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint hIcon);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DrawIconEx(
        nint hdc, int xLeft, int yTop, nint hIcon,
        int cxWidth, int cyHeight,
        uint istepIfAniCur, nint hbrFlickerFreeDraw, uint diFlags);

    public const uint DI_NORMAL = 0x0003;

    // ── SendInput ─────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint    type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT   ki;
        [FieldOffset(0)] public MOUSEINPUT   mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public nint   dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int   dx, dy;
        public uint  mouseData;
        public uint  dwFlags;
        public uint  time;
        public nint  dwExtraInfo;
    }

    [LibraryImport("user32.dll")]
    public static partial uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    // ── Media keys ────────────────────────────────────────────────────────

    [LibraryImport("user32.dll")]
    public static partial nint keybd_event(byte bVk, byte bScan, uint dwFlags, nint dwExtraInfo);

    // ── Foreground app ────────────────────────────────────────────────────

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hWnd);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryFullProcessImageName(nint hProcess, uint dwFlags, Span<char> lpExeName, ref uint lpdwSize);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    /// <summary>Process name (exe file name without extension) for a pid, or null. A single
    /// kernel call with no System.Diagnostics.Process involvement, so it is safe to call from
    /// the hook thread; works for elevated processes too (limited query access).</summary>
    public static string? GetProcessNameByPid(uint pid)
    {
        if (pid == 0) return null;
        nint h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == 0) return null;
        try
        {
            Span<char> buf = stackalloc char[1024];
            uint len = (uint)buf.Length;
            if (!QueryFullProcessImageName(h, 0, buf, ref len) || len == 0) return null;
            return Path.GetFileNameWithoutExtension(new string(buf[..(int)len]));
        }
        finally { CloseHandle(h); }
    }

    [DllImport("user32.dll")]
    public static extern nint CallWindowProcW(nint lpPrevWndFunc, nint hWnd, uint Msg, nint wParam, nint lParam);

    // ── Window style constants ────────────────────────────────────────────
    public const int  GWLP_WNDPROC       = -4;
    public const int  GWL_EXSTYLE        = -20;
    public const uint WS_EX_LAYERED      = 0x00080000;
    public const uint WS_EX_TRANSPARENT  = 0x00000020;
    public const uint WS_EX_TOPMOST      = 0x00000008;
    public const uint WS_EX_NOACTIVATE   = 0x08000000;
    public const uint WS_EX_TOOLWINDOW   = 0x00000080;
    public const uint WS_EX_APPWINDOW    = 0x00040000;
    public const uint WM_SYSCOMMAND      = 0x0112;
    public const nint SC_MINIMIZE        = 0xF020;
    public const int  SW_HIDE            = 0;
    public const int  SW_SHOW            = 5;
    public const int  SW_RESTORE         = 9;
    public const uint SWP_NOMOVE         = 0x0002;
    public const uint SWP_NOSIZE         = 0x0001;
    public const uint SWP_NOACTIVATE     = 0x0010;
    public const nint HWND_TOPMOST       = -1;
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    // ── Raw window creation ───────────────────────────────────────────────

    public delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEXW
    {
        public uint  cbSize;
        public uint  style;
        public nint  lpfnWndProc;
        public int   cbClsExtra;
        public int   cbWndExtra;
        public nint  hInstance;
        public nint  hIcon;
        public nint  hCursor;
        public nint  hbrBackground;
        public nint  lpszMenuName;
        public nint  lpszClassName;
        public nint  hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int X, int Y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [LibraryImport("user32.dll")]
    public static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern nint LoadCursorW(nint hInstance, int lpCursorName);

    public const int IDC_ARROW = 32512;

    // ── GDI (layered window rendering) ───────────────────────────────────

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint h);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint   biSize;
        public int    biWidth;
        public int    biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint   biCompression;
        public uint   biSizeImage;
        public int    biXPelsPerMeter;
        public int    biYPelsPerMeter;
        public uint   biClrUsed;
        public uint   biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint             bmiColors; // unused for BI_RGB, included for struct alignment
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern nint CreateDIBSection(
        nint hdc, ref BITMAPINFO pbmi, uint usage,
        out nint ppvBits, nint hSection, uint offset);

    [StructLayout(LayoutKind.Sequential)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int cx, cy; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateLayeredWindow(
        nint hwnd, nint hdcDst,
        ref POINT pptDst, ref SIZE psize,
        nint hdcSrc, ref POINT pptSrc,
        uint crKey, ref BLENDFUNCTION pblend,
        uint dwFlags);

    // ── Multimedia timer resolution ───────────────────────────────────────
    // Call timeBeginPeriod(1) on startup to set the Windows scheduler interrupt
    // period to 1ms. Without this, Thread.Sleep resolution is ~15.625ms (the default
    // timer tick), which causes the render thread to overshoot frame intervals badly.
    // timeEndPeriod(1) must be called on shutdown to restore the default resolution.

    [DllImport("winmm.dll")]
    public static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll")]
    public static extern uint timeEndPeriod(uint uPeriod);

    // ── Window style constants (additional) ───────────────────────────────
    public const uint WS_POPUP        = 0x80000000u;
    public const uint ULW_ALPHA       = 0x00000002u;
    public const byte AC_SRC_OVER    = 0x00;
    public const byte AC_SRC_ALPHA   = 0x01;
    public const uint BI_RGB          = 0u;
    public const uint DIB_RGB_COLORS  = 0u;

    // ── Helper: get monitor info from a screen point ──────────────────────

    public static (RECT bounds, float dpiScale) GetMonitorInfoForPoint(POINT pt)
    {
        var hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(hMonitor, ref info);
        GetDpiForMonitor(hMonitor, 0, out uint dpiX, out _);
        float scale = dpiX / 96f;
        return (info.rcMonitor, scale);
    }
}
