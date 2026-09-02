// AeroDial — OverlayWindow.cs
// Transparent always-on-top overlay window — raw Win32 HWND.
// Uses WS_EX_LAYERED + UpdateLayeredWindow for true per-pixel alpha
// transparency. WinUI 3's DirectComposition root visual is inherently opaque
// (DwmExtendFrameIntoClientArea cannot overcome it), so we bypass the WinUI
// compositor entirely and talk directly to DWM via UpdateLayeredWindow.
// The Skia renderer writes into the DIBSection pixel memory and
// UpdateLayeredWindow delivers it to DWM with premultiplied alpha.

using System.Runtime.InteropServices;
using AeroDial.Config;
using AeroDial.Core;

namespace AeroDial.Overlay;

internal sealed class OverlayWindow : IDisposable
{
    public event Action<int>? HoveredIndexChanged;
    public event Action<int>? ItemClicked;
    public event Action?      CenterClicked;
    public event Action<int>? ChildItemClicked;
    public event Action<int>? ChildHoveredIndexChanged;
    public event Action<int>? L3ItemClicked;
    public event Action<int>? L3HoveredIndexChanged;
    public event Action?      ClickedOutside;

    private readonly OverlayRenderer _renderer;
    private nint _hwnd;
    private bool _disposed;

    // Current window position in screen coords (physical pixels)
    private int _winX, _winY, _winSize;

    // Window class — registered once per process lifetime
    private const string OverlayClassName = "AeroDial_Overlay_Win32";
    private static ushort _classAtom;
    private static readonly Win32.WndProcDelegate _wndProc = StaticWndProc;
    private static readonly object _classLock = new();

    public OverlayWindow(OverlayController controller)
    {
        _renderer = new OverlayRenderer();
        _renderer.HoveredIndexChanged      += idx => HoveredIndexChanged?.Invoke(idx);
        _renderer.ItemClicked              += idx => ItemClicked?.Invoke(idx);
        _renderer.CenterClicked            += ()  => CenterClicked?.Invoke();
        _renderer.ChildItemClicked         += idx => ChildItemClicked?.Invoke(idx);
        _renderer.ChildHoveredIndexChanged += idx => ChildHoveredIndexChanged?.Invoke(idx);
        _renderer.L3ItemClicked            += idx => L3ItemClicked?.Invoke(idx);
        _renderer.L3HoveredIndexChanged    += idx => L3HoveredIndexChanged?.Invoke(idx);
        _renderer.ClickedOutside           += ()  => ClickedOutside?.Invoke();
    }

    public void Show(System.Drawing.Point cursorPos, RadialMenuConfig menu, bool hasParent = false)
    {
        var pt = new Win32.POINT { X = cursorPos.X, Y = cursorPos.Y };
        var (monitorBounds, dpiScale) = Win32.GetMonitorInfoForPoint(pt);

        EnsureWindow();
        PositionWindow(cursorPos, monitorBounds, dpiScale);

        _renderer.SetHwnd(_hwnd);
        _renderer.UpdateWindowRect(_winX, _winY, _winSize);
        _renderer.BeginShow(menu, cursorPos, dpiScale, hasParent);
        _renderer.PreRender(); // fill layered window before it becomes visible — eliminates blank-frame flicker

        Win32.ShowWindow(_hwnd, Win32.SW_SHOW);
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
    }

    public void Hide()
    {
        _renderer.BeginHide(() =>
        {
            if (_hwnd != 0)
                Win32.ShowWindow(_hwnd, Win32.SW_HIDE);
        });
    }

    public void NavigateTo(RadialMenuConfig menu, bool hasParent)
        => _renderer.NavigateTo(menu, hasParent);

    public void ShowChildMenu(RadialMenuConfig menu, int parentIndex)
        => _renderer.ShowChildMenu(menu, parentIndex);

    public void HideChildMenu()
        => _renderer.HideChildMenu();

    public void ShowL3Menu(RadialMenuConfig menu, int l2ParentIndex)
        => _renderer.ShowL3Menu(menu, l2ParentIndex);

    public void HideL3Menu()
        => _renderer.HideL3Menu();

    /// <summary>Swaps the child ring's menu in place (no pop-out animation restart).</summary>
    public void ReplaceChildMenu(RadialMenuConfig menu)
        => _renderer.ReplaceChildMenu(menu);

    /// <summary>Swaps the main ring's menu in place.</summary>
    public void ReplaceMenu(RadialMenuConfig menu)
        => _renderer.ReplaceMenu(menu);

    /// <summary>Flashes the volume arc on a scroll-wheel volume action.</summary>
    public void TriggerVolumeFlash()
        => _renderer.TriggerVolumeFlash();

    // ── Window lifecycle ──────────────────────────────────────────────────

    private void EnsureWindow()
    {
        if (_hwnd != 0) return;

        EnsureWindowClass();

        nint hInstance = Win32.GetModuleHandleW(null);
        uint exStyle   = Win32.WS_EX_LAYERED
                       | Win32.WS_EX_TOPMOST
                       | Win32.WS_EX_NOACTIVATE
                       | Win32.WS_EX_TOOLWINDOW;

        _hwnd = Win32.CreateWindowExW(
            exStyle, OverlayClassName, "",
            Win32.WS_POPUP,
            0, 0, 10, 10,
            0, 0, hInstance, 0);

        if (_hwnd == 0)
            Logger.Error($"CreateWindowExW failed: {Marshal.GetLastWin32Error()}");
    }

    private void PositionWindow(
        System.Drawing.Point cursor,
        Win32.RECT monitor,
        float dpiScale)
    {
        float scale      = App.Config.Current.Appearance.Scale;
        int physicalSize = (int)(AppConstants.CanvasSize * dpiScale * scale);
        int halfSize     = physicalSize / 2;

        int x = Math.Clamp(cursor.X - halfSize, monitor.Left, monitor.Right  - physicalSize);
        int y = Math.Clamp(cursor.Y - halfSize, monitor.Top,  monitor.Bottom - physicalSize);

        _winX    = x;
        _winY    = y;
        _winSize = physicalSize;
        Logger.Debug($"Overlay window rect: ({x},{y}) size={physicalSize} dpi={dpiScale} cursor=({cursor.X},{cursor.Y})");

        // Set correct position/size before ShowWindow so window appears in the right place
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, x, y, physicalSize, physicalSize,
            Win32.SWP_NOACTIVATE);
    }

    // ── Window class registration (once per process) ──────────────────────

    private static void EnsureWindowClass()
    {
        lock (_classLock)
        {
            if (_classAtom != 0) return;

            nint wndProcPtr  = Marshal.GetFunctionPointerForDelegate(_wndProc);
            nint hInstance   = Win32.GetModuleHandleW(null);
            // Intentional: pointer must stay valid for the process lifetime once registered.
            // Marshal.StringToHGlobalUni is not freed — acceptable for a one-time class name.
            nint classNamePtr = Marshal.StringToHGlobalUni(OverlayClassName);

            var wc = new Win32.WNDCLASSEXW
            {
                cbSize        = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
                lpfnWndProc   = wndProcPtr,
                hInstance     = hInstance,
                lpszClassName = classNamePtr,
                hCursor       = Win32.LoadCursorW(0, Win32.IDC_ARROW), // prevent busy cursor leaking into overlay
            };

            _classAtom = Win32.RegisterClassExW(ref wc);
            if (_classAtom == 0)
                Logger.Error($"RegisterClassExW failed: {Marshal.GetLastWin32Error()}");
        }
    }

    // Default window proc — all messages handled by DefWindowProcW
    private static nint StaticWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
        => Win32.DefWindowProcW(hWnd, msg, wParam, lParam);

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _renderer.Dispose();
        if (_hwnd != 0)
        {
            Win32.DestroyWindow(_hwnd);
            _hwnd = 0;
        }
    }
}
