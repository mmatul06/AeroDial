// AeroDial — BehaviorPage.cs
// Split from SettingsPages.cs: one settings page per file.

using AeroDial.Config;
using AeroDial.Core;
using AeroDial.Overlay;
using AeroDial.Themes;
using AeroDial.UI.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace AeroDial.UI.Views.Pages;

// ═══════════════════════════════════════════════════════════════════════════
// BehaviorPage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class BehaviorPage : Page
{
    // Hold-mode controls
    private ToggleSwitch? _launch;  // only created in Hold mode

    // Toggle-mode controls
    private ComboBox     _mode  = null!;
    private Slider       _dwell = null!;
    private StackPanel   _dwellRow = null!;

    // Always-visible controls
    private ToggleSwitch _close = null!, _startup = null!, _closeOutside = null!;
    private TextBlock    _saved = null!;
    private DispatcherTimer? _saveTimer;

    public BehaviorPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };
        var cfg    = App.Config.Current.Behavior;
        bool holdMode = App.Config.Current.Trigger.HoldMode;

        stack.Children.Add(PageKit.PageHeader("Behavior"));

        // ── Item selection ────────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Item selection"));

        if (holdMode)
        {
            // Hold mode: trigger release is the primary mechanism, but dwell is also available.
            _launch = new ToggleSwitch
            {
                Header     = "Execute highlighted item on trigger release",
                IsOn       = cfg.LaunchOnRelease,
                OnContent  = "On: release trigger to confirm",
                OffContent = "Off",
            };
            stack.Children.Add(_launch);
            _launch.Toggled += (_, _) => ScheduleSave();

            _mode = new ComboBox
            {
                Width       = 300,
                ItemsSource = new[]
                {
                    "Click: left-click an item",
                    "Hover dwell: auto-executes after a delay",
                },
                SelectedIndex = cfg.SelectionMode == AeroDial.Config.SelectionMode.HoverDwell ? 1 : 0,
            };
            stack.Children.Add(_mode);

            _dwellRow = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
            _dwell    = PageKit.MakeSlider("Hover dwell delay (ms)", 100, 1500, 50, cfg.HoverDwellMs);
            _dwellRow.Children.Add(_dwell);
            stack.Children.Add(_dwellRow);
            _dwellRow.Visibility = cfg.SelectionMode == AeroDial.Config.SelectionMode.HoverDwell
                ? Visibility.Visible : Visibility.Collapsed;

            _mode.SelectionChanged += (_, _) =>
                _dwellRow.Visibility = _mode.SelectedIndex == 1
                    ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            // Toggle mode: trigger only opens/closes; user picks how items are selected.
            stack.Children.Add(PageKit.InfoCard(
                "In Toggle mode, items are selected by the method below. Not by the trigger button."));

            _mode = new ComboBox
            {
                Width       = 300,
                ItemsSource = new[]
                {
                    "Hover: auto-executes after a delay",
                    "Click: left-click an item",
                    "Flick: aim cursor direction, tap trigger to confirm",
                },
                SelectedIndex = (int)cfg.SelectionMode,
            };
            stack.Children.Add(_mode);

            _dwellRow = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
            _dwell = PageKit.MakeSlider("Hover dwell delay (ms)", 100, 1500, 50, cfg.HoverDwellMs);
            _dwellRow.Children.Add(_dwell);
            stack.Children.Add(_dwellRow);
            _dwellRow.Visibility = cfg.SelectionMode == AeroDial.Config.SelectionMode.HoverDwell
                ? Visibility.Visible : Visibility.Collapsed;

            _mode.SelectionChanged += (_, _) =>
                _dwellRow.Visibility = _mode.SelectedIndex == 0
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── After executing ───────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("After executing"));
        _close = new ToggleSwitch
        {
            Header    = "Close menu after executing an action",
            IsOn      = cfg.CloseOnActionExecuted,
            OnContent = "On", OffContent = "Off: menu stays open",
        };
        stack.Children.Add(_close);

        _closeOutside = new ToggleSwitch
        {
            Header     = "Close menu when clicking outside",
            IsOn       = cfg.CloseOnClickOutside,
            OnContent  = "On", OffContent = "Off",
        };
        stack.Children.Add(_closeOutside);

        // ── System ────────────────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("System"));
        _startup = new ToggleSwitch
        {
            Header    = "Start AeroDial with Windows",
            IsOn      = cfg.StartWithWindows,
            OnContent = "On", OffContent = "Off",
        };
        stack.Children.Add(_startup);

        // ── Reset ─────────────────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Reset"));
        stack.Children.Add(PageKit.InfoCard(
            "Restores all trigger, appearance, and behavior settings to factory defaults. " +
            "Menus are not changed. Reopen Settings to see the updated values."));
        var resetBtn = new Button
        {
            Content    = "Restore all settings to default",
            Style      = (Style)Application.Current.Resources["AccentButtonStyle"],
            Margin     = new Thickness(0, 4, 0, 0),
        };
        var resetStatus = new TextBlock
        {
            Text       = "Settings restored. Reopen Settings to see updated values.",
            FontSize   = 12,
            Foreground = Ui.Success,
            Visibility = Visibility.Collapsed,
            Margin     = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        resetBtn.Click += async (_, _) =>
        {
            var dlg = new ContentDialog
            {
                Title             = "Restore defaults",
                Content           = "This will reset all trigger, appearance, and behavior settings to factory defaults. Your menus will not be changed. Continue?",
                PrimaryButtonText = "Restore",
                CloseButtonText   = "Cancel",
                XamlRoot          = XamlRoot,
            };
            var result = await dlg.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            await App.Config.UpdateAsync(cfg =>
            {
                var d = new AeroDialConfig();
                cfg.Trigger    = d.Trigger;
                cfg.Appearance = d.Appearance;
                cfg.Behavior   = d.Behavior;
            });
            resetStatus.Visibility = Visibility.Visible;
            resetBtn.IsEnabled = false;
        };
        stack.Children.Add(resetBtn);
        stack.Children.Add(resetStatus);

        // Auto-save wiring
        _mode.SelectionChanged  += (_, _) => ScheduleSave();
        _dwell.ValueChanged     += (_, _) => ScheduleSave();
        _close.Toggled          += (_, _) => ScheduleSave();
        _closeOutside.Toggled   += (_, _) => ScheduleSave();
        _startup.Toggled        += (_, _) => ScheduleSave();

        _saved = PageKit.SavedBadge();
        _saved.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_saved);

        scroll.Content = stack; Content = scroll;
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        bool holdMode = App.Config.Current.Trigger.HoldMode;
        await App.Config.UpdateAsync(cfg =>
        {
            if (holdMode)
            {
                cfg.Behavior.LaunchOnRelease = _launch!.IsOn;
                cfg.Behavior.HoverDwellMs    = (int)_dwell.Value;
                // 0=Click, 1=HoverDwell
                cfg.Behavior.SelectionMode   = _mode.SelectedIndex == 1
                    ? AeroDial.Config.SelectionMode.HoverDwell
                    : AeroDial.Config.SelectionMode.Click;
            }
            else
            {
                // In Toggle mode, release-execute doesn't apply.
                cfg.Behavior.SelectionMode   = (AeroDial.Config.SelectionMode)_mode.SelectedIndex;
                cfg.Behavior.HoverDwellMs    = (int)_dwell.Value;
                cfg.Behavior.LaunchOnRelease = false;
            }
            cfg.Behavior.CloseOnActionExecuted = _close.IsOn;
            cfg.Behavior.CloseOnClickOutside   = _closeOutside.IsOn;
            cfg.Behavior.StartWithWindows      = _startup.IsOn;
        });
        ApplyStartup(App.Config.Current.Behavior.StartWithWindows);
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
    }

    private static void ApplyStartup(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (enable)
                key?.SetValue(AppConstants.AppName,
                    $"\"{System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName}\"");
            else
                key?.DeleteValue(AppConstants.AppName, throwOnMissingValue: false);
        }
        catch (Exception ex) { Logger.Warn("Could not apply startup setting", ex); }
    }

    private void ScheduleSave()
    {
        _saveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Stop();
        _saveTimer.Tick -= OnSaveTimerTick;
        _saveTimer.Tick += OnSaveTimerTick;
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? s, object e)
    {
        _saveTimer!.Stop();
        Save(null!, null!);
    }

}
