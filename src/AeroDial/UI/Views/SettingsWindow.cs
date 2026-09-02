// AeroDial — SettingsWindow.cs
// Settings window built entirely in code — no XAML dependency.
// Windows 11 native look: Mica backdrop, NavigationView rail with icon-font glyphs,
// and every color from the theme resources so it follows the system light/dark
// setting and accent color. Uses ContentControl instead of Frame so pages can be
// pure code-behind without requiring XAML-backed InitializeComponent.

using System.Runtime.InteropServices;
using AeroDial.Core;
using AeroDial.UI.Views.Pages;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AeroDial.UI.Views;

public sealed class SettingsWindow : Window
{
    private static SettingsWindow? _instance;

    /// <summary>Win32 HWND of the settings window — needed to initialize WinRT file pickers.</summary>
    public static nint WindowHandle { get; private set; }

    // WndProc subclass — keeps delegate alive (prevents GC collection)
    private static Win32.WndProcDelegate? _wndProcDelegate;
    private static nint _prevWndProc;

    private const int WindowWidth  = 1120;  // logical px (see ConfigureChrome)
    private const int WindowHeight = 760;

    private readonly NavigationView _nav;
    private readonly ContentControl _contentFrame;
    private string _currentTag = "trigger";

    /// <summary>All navigation tags in sidebar order (used by the self-test to walk every page).</summary>
    internal static readonly string[] PageTags =
        ["trigger", "appearance", "behavior", "menus", "profiles", "themes", "advanced", "about"];

    // (tag, label, Segoe Fluent Icons glyph)
    private static readonly (string Tag, string Label, string Glyph)[] NavItems =
    [
        ("trigger",    "Trigger",      "E962"), // mouse
        ("appearance", "Appearance",   "E7F4"), // monitor
        ("behavior",   "Behavior",     "E945"), // lightning bolt
        ("menus",      "Menus",        "E8FD"), // list
        ("profiles",   "App profiles", "E71D"), // all apps
        ("themes",     "Themes",       "E790"), // color
        ("advanced",   "Advanced",     "E713"), // settings gear
    ];

    // ── Static entry point ────────────────────────────────────────────────

    public static void ShowOrActivate()
    {
        if (_instance is null)
        {
            _instance = new SettingsWindow();
            _instance.Closed += (_, _) => _instance = null;
        }

        // Always run the full show sequence, including on first open. From the tray
        // thread a bare Activate() can leave the window minimized / behind, so:
        //  - Activate() shows the WinUI window,
        //  - SW_RESTORE un-hides (X pressed) AND un-minimizes (─ pressed),
        //  - SetForegroundWindow pulls it to the front from a non-foreground caller.
        _instance.Activate();
        Win32.ShowWindow(WindowHandle, Win32.SW_RESTORE);
        Win32.SetForegroundWindow(WindowHandle);
    }

    /// <summary>Current window instance, if open (self-test only).</summary>
    internal static SettingsWindow? Instance => _instance;

    // ── Constructor ───────────────────────────────────────────────────────

    public SettingsWindow()
    {
        Title = "AeroDial Settings";

        // Mica (falls back to acrylic, then to the plain theme background) — the WinUI
        // controls and Ui brushes all resolve through the theme dictionaries, so the
        // window follows the Windows light/dark setting without any code here.
        SystemBackdrop = MakeBackdrop();

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ── Custom title bar (drag region) ────────────────────────────────
        var titleBar = new Grid { Height = 40, Padding = new Thickness(16, 0, 0, 0) };
        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleRow.Children.Add(BuildBrandIcon(18));
        titleRow.Children.Add(new TextBlock
        {
            Text = "AeroDial", FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Ui.TextSecondary,
        });
        titleBar.Children.Add(titleRow);
        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

        // ── Navigation rail ───────────────────────────────────────────────
        _nav = new NavigationView
        {
            PaneDisplayMode              = NavigationViewPaneDisplayMode.Left,
            OpenPaneLength               = 220,
            IsSettingsVisible            = false,
            IsBackButtonVisible          = NavigationViewBackButtonVisible.Collapsed,
            IsPaneToggleButtonVisible    = false,
            IsTitleBarAutoPaddingEnabled = false,
        };

        var paneHeader = new StackPanel { Padding = new Thickness(12, 8, 12, 12), Spacing = 2 };
        paneHeader.Children.Add(new TextBlock
        {
            Text = "AeroDial", FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        paneHeader.Children.Add(new TextBlock
        {
            Text = $"Version {AppConstants.Version}", FontSize = 12, Foreground = Ui.TextSecondary,
        });
        _nav.PaneHeader = paneHeader;

        foreach (var (tag, label, glyph) in NavItems)
            _nav.MenuItems.Add(new NavigationViewItem { Content = label, Tag = tag, Icon = Ui.Glyph(glyph) });
        _nav.FooterMenuItems.Add(new NavigationViewItem { Content = "About", Tag = "about", Icon = Ui.Glyph("E946") });

        _nav.SelectionChanged += (_, e) =>
        {
            if (e.SelectedItem is NavigationViewItem item && item.Tag is string tag)
                Navigate(tag);
        };

        // ── Content area (ContentControl instead of Frame) ────────────────
        _contentFrame = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment   = VerticalAlignment.Stretch,
        };
        _nav.Content = _contentFrame;

        Grid.SetRow(_nav, 1);
        root.Children.Add(_nav);
        Content = root;

        WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ConfigureChrome(titleBar);
        InstallWndProc();
        // No AppWindow.Closing handler — WM_CLOSE is intercepted in the WndProc
        // to hide the window to the tray instead of destroying it.

        // Pages are built in code with brushes resolved at construction time, so rebuild
        // the visible page when Windows switches between light and dark.
        root.ActualThemeChanged += (_, _) => Navigate(_currentTag);

        // Select first item after layout is ready
        DispatcherQueue.TryEnqueue(() => _nav.SelectedItem = _nav.MenuItems[0]);

        Logger.Info("SettingsWindow opened.");
    }

    private static SystemBackdrop? MakeBackdrop()
    {
        try
        {
            if (MicaController.IsSupported()) return new MicaBackdrop { Kind = MicaKind.Base };
            if (DesktopAcrylicController.IsSupported()) return new DesktopAcrylicBackdrop();
        }
        catch (Exception ex) { Logger.Warn("System backdrop unavailable", ex); }
        return null;
    }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>Selects a page as if the user clicked it in the rail.</summary>
    internal void NavigateTo(string tag)
    {
        foreach (var it in _nav.MenuItems.Concat(_nav.FooterMenuItems))
            if (it is NavigationViewItem nvi && (string?)nvi.Tag == tag) { _nav.SelectedItem = nvi; return; }
    }

    private void Navigate(string tag)
    {
        _currentTag = tag;
        // Instantiate pages directly — avoids Frame.Navigate() which crashes
        // when pages are built in pure code without XAML-generated InitializeComponent.
        UIElement content = tag switch
        {
            "trigger"    => new TriggerPage(),
            "appearance" => new AppearancePage(),
            "behavior"   => new BehaviorPage(),
            "menus"      => new MenusPage(),
            "profiles"   => new ProfilesPage(),
            "themes"     => new ThemesPage(),
            "advanced"   => new AdvancedPage(),
            "about"      => new AboutPage(),
            _            => new TriggerPage(),
        };
        _contentFrame.Content = content;
    }

    // ── Window proc — X hides to tray, minimize works normally ───────────

    private const uint WM_CLOSE     = 0x0010;
    private const uint WM_NCDESTROY = 0x0082;

    private void InstallWndProc()
    {
        // The subclass state is static. If a previous window is somehow still subclassed,
        // installing again would chain the WndProc to itself and recurse until the stack
        // overflows, so refuse rather than risk it.
        if (_prevWndProc != 0)
        {
            Logger.Warn("SettingsWindow: WndProc already installed; skipping subclass.");
            return;
        }
        _wndProcDelegate = SettingsWndProc;
        _prevWndProc = Win32.SetWindowLongPtrW(
            WindowHandle, Win32.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    private static nint SettingsWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_CLOSE)
        {
            // Hide to tray instead of destroying the window.
            // The window object is reused; ShowOrActivate() restores it via SW_RESTORE.
            Win32.ShowWindow(hWnd, Win32.SW_HIDE);
            return 0;
        }
        if (msg == WM_NCDESTROY)
        {
            // Window really is going away: restore the original WndProc and clear the
            // static state so a future SettingsWindow can subclass its own HWND cleanly.
            nint prev = _prevWndProc;
            _prevWndProc = 0;
            Win32.SetWindowLongPtrW(hWnd, Win32.GWLP_WNDPROC, prev);
            return Win32.CallWindowProcW(prev, hWnd, msg, wParam, lParam);
        }
        // SC_MINIMIZE passes through unmodified — window minimizes to taskbar normally.
        return Win32.CallWindowProcW(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    // ── Brand icon ────────────────────────────────────────────────────────

    /// <summary>The app icon (Assets/aerodial.ico) at the given size; a plain glyph if missing.</summary>
    internal static UIElement BuildBrandIcon(double size)
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "aerodial.ico");
            if (File.Exists(iconPath))
                return new Image
                {
                    Source  = new BitmapImage(new Uri(iconPath)),
                    Width   = size, Height = size,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                };
        }
        catch { /* icon file missing or corrupt — use fallback */ }

        var glyph = Ui.Glyph("E76D", size); // dial
        glyph.VerticalAlignment = VerticalAlignment.Center;
        return glyph;
    }

    // ── Chrome ────────────────────────────────────────────────────────────

    private void ConfigureChrome(UIElement dragRegion)
    {
        // AppWindow sizes are physical pixels; WindowWidth/Height are the logical layout size,
        // so scale by the monitor DPI (at 125 % a 1120 px layout needs a 1400 px window).
        // Without this the window shrinks to 80 % of its layout at 125 % and pages get cramped.
        float dpiScale = Math.Max(1f, Win32.GetDpiForWindow(WindowHandle) / 96f);
        int   physW    = (int)Math.Round(WindowWidth  * dpiScale);
        int   physH    = (int)Math.Round(WindowHeight * dpiScale);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(physW, physH));
        AppWindow.IsShownInSwitchers = true;

        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "aerodial.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(dragRegion);
            AppWindow.TitleBar.ButtonBackgroundColor         = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        var display = DisplayArea.Primary;
        int x = (display.WorkArea.Width  - physW) / 2;
        int y = (display.WorkArea.Height - physH) / 2;
        AppWindow.Move(new Windows.Graphics.PointInt32(Math.Max(0, x), Math.Max(0, y)));
    }
}
