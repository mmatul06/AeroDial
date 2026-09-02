// AeroDial — SettingsWindow.cs
// Settings window built entirely in code — no XAML dependency.
// Uses ContentControl instead of Frame so pages can be pure code-behind
// without requiring XAML-backed InitializeComponent.

using System.Runtime.InteropServices;
using AeroDial.Core;
using AeroDial.UI.Views.Pages;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AeroDial.UI.Views;

public sealed class SettingsWindow : Window
{
    private static SettingsWindow? _instance;

    /// <summary>Win32 HWND of the settings window — needed to initialize WinRT file pickers.</summary>
    public static nint WindowHandle { get; private set; }

    // WndProc subclass — keeps delegate alive (prevents GC collection)
    private static Win32.WndProcDelegate? _wndProcDelegate;
    private static nint _prevWndProc;

    private readonly ListView      _navList;
    private readonly ContentControl _contentFrame;

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

    // ── Constructor ───────────────────────────────────────────────────────

    public SettingsWindow()
    {
        Title = "AeroDial — Settings";

        // ── Root layout ───────────────────────────────────────────────────
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ── Sidebar ───────────────────────────────────────────────────────
        var sidebar = new Grid
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 28, 28, 40)),
        };
        sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Branding header
        var brandRow = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            Spacing           = 10,
            Padding           = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        UIElement brandDotGrid = BuildBrandIcon();

        var brandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        brandText.Children.Add(new TextBlock
        {
            Text       = "AeroDial",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize   = 15,
        });
        brandText.Children.Add(new TextBlock
        {
            Text       = $"v{AppConstants.Version}",
            FontSize   = 11,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(150, 200, 200, 220)),
        });

        brandRow.Children.Add(brandDotGrid);
        brandRow.Children.Add(brandText);
        Grid.SetRow(brandRow, 0);
        sidebar.Children.Add(brandRow);

        // Nav list
        _navList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Padding       = new Thickness(8, 4, 8, 4),
        };

        foreach (var (tag, label) in new[]
        {
            ("trigger",      "Trigger"),
            ("appearance",   "Appearance"),
            ("behavior",     "Behavior"),
            ("dynamic",      "Dynamic"),
            ("menus",        "Menus"),
            ("profiles",     "App Profiles"),
            ("themes",       "Themes"),
            ("theme_editor", "Theme Editor"),
            ("advanced",     "Advanced"),
            ("about",        "About"),
        })
        {
            _navList.Items.Add(new ListViewItem { Tag = tag, Content = label });
        }

        _navList.SelectionChanged += OnNavSelectionChanged;
        Grid.SetRow(_navList, 1);
        sidebar.Children.Add(_navList);

        // Footer
        var footer = new TextBlock
        {
            Text                = "3M Design Solutions",
            FontSize            = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground          = new SolidColorBrush(ColorHelper.FromArgb(80, 200, 200, 220)),
            Margin              = new Thickness(0, 0, 0, 12),
        };
        Grid.SetRow(footer, 2);
        sidebar.Children.Add(footer);

        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        // ── Content area (ContentControl instead of Frame) ────────────────
        _contentFrame = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment   = VerticalAlignment.Stretch,
        };
        Grid.SetColumn(_contentFrame, 1);
        root.Children.Add(_contentFrame);

        Content = root;

        WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ConfigureChrome();
        InstallWndProc();
        // No AppWindow.Closing handler — WM_CLOSE is intercepted in the WndProc
        // to hide the window to the tray instead of destroying it.

        // Select first item after layout is ready
        DispatcherQueue.TryEnqueue(() =>
        {
            _navList.SelectedIndex = 0;
        });

        Logger.Info("SettingsWindow opened.");
    }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>All navigation tags in sidebar order (used by the self-test to walk every page).</summary>
    internal static readonly string[] PageTags =
        ["trigger", "appearance", "behavior", "dynamic", "menus", "profiles", "themes", "theme_editor", "advanced", "about"];

    /// <summary>Current window instance, if open (self-test only).</summary>
    internal static SettingsWindow? Instance => _instance;

    /// <summary>Selects a page as if the user clicked it in the sidebar.</summary>
    internal void NavigateTo(string tag)
    {
        for (int i = 0; i < _navList.Items.Count; i++)
            if (_navList.Items[i] is ListViewItem li && (string?)li.Tag == tag) { _navList.SelectedIndex = i; return; }
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navList.SelectedItem is ListViewItem item && item.Tag is string tag)
            Navigate(tag);
    }

    private void Navigate(string tag)
    {
        // Instantiate pages directly — avoids Frame.Navigate() which crashes
        // when pages are built in pure code without XAML-generated InitializeComponent.
        UIElement content = tag switch
        {
            "trigger"      => new TriggerPage(),
            "appearance"   => new AppearancePage(),
            "behavior"     => new BehaviorPage(),
            "dynamic"      => new DynamicPage(),
            "menus"        => new MenusPage(),
            "profiles"     => new ProfilesPage(),
            "themes"       => new ThemesPage(),
            "theme_editor" => new ThemeEditorPage(),
            "advanced"     => new AdvancedPage(),
            "about"        => new AboutPage(),
            _              => new TriggerPage(),
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

    /// <summary>
    /// Returns a 32×32 brand icon for the sidebar. Loads Assets/aerodial.ico when present;
    /// falls back to the purple "A" circle so the app still looks polished without an icon file.
    /// </summary>
    private static UIElement BuildBrandIcon()
    {
        try
        {
            var iconPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "aerodial.ico");
            if (File.Exists(iconPath))
                return new Image
                {
                    Source  = new BitmapImage(new Uri(iconPath)),
                    Width   = 32,
                    Height  = 32,
                    Stretch = Stretch.Uniform,
                };
        }
        catch { /* icon file missing or corrupt — use fallback */ }

        // Fallback: purple circle with "A"
        var dot = new Border
        {
            Width        = 32,
            Height       = 32,
            CornerRadius = new CornerRadius(16),
            Background   = new SolidColorBrush(ColorHelper.FromArgb(255, 124, 110, 247)),
        };
        var text = new TextBlock
        {
            Text                = "A",
            FontSize            = 16,
            FontWeight          = Microsoft.UI.Text.FontWeights.Bold,
            Foreground          = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        var grid = new Grid { Width = 32, Height = 32 };
        grid.Children.Add(dot);
        grid.Children.Add(text);
        return grid;
    }

    // ── Chrome ────────────────────────────────────────────────────────────

    private void ConfigureChrome()
    {
        AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 680));
        AppWindow.IsShownInSwitchers = true;

        var iconPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets", "aerodial.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.ButtonBackgroundColor         = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        var display = DisplayArea.Primary;
        int x = (display.WorkArea.Width  - 900) / 2;
        int y = (display.WorkArea.Height - 680) / 2;
        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }
}
