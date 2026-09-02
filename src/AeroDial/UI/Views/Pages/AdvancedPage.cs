// AeroDial — AdvancedPage.cs
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
// AdvancedPage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class AdvancedPage : Page
{
    private ToggleSwitch _thinning = null!, _partialArc = null!, _debugLog = null!;
    private TextBlock    _saved    = null!;
    private DispatcherTimer? _saveTimer;

    public AdvancedPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };
        var cfg    = App.Config.Current.Appearance;

        stack.Children.Add(PageKit.PageHeader("Advanced"));
        stack.Children.Add(PageKit.InfoCard(
            "These settings control 3-level menu depth and arc layout. " +
            "Defaults are tuned for the best out-of-box experience."));

        // ── Multi-level ring rendering ─────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Multi-level rings"));

        _thinning = new ToggleSwitch
        {
            Header     = "Dynamic ring thinning",
            IsOn       = cfg.DynamicRingThinning,
            OnContent  = "On: L2 ring narrows when an L3 ring is also visible",
            OffContent = "Off: all rings stay at full width",
        };

        _partialArc = new ToggleSwitch
        {
            Header     = "Partial arc submenus",
            IsOn       = cfg.PartialArcSubMenu,
            OnContent  = "On: child rings fan out only around the parent slice angle",
            OffContent = "Off: child rings are always full 360°",
        };

        stack.Children.Add(_thinning);
        stack.Children.Add(_partialArc);

        stack.Children.Add(PageKit.InfoCard(
            "Partial arc: with 4 items the ring fans around the parent, " +
            "making the parent-child relationship visually clear. " +
            "With 8+ items it automatically falls back to full 360°."));

        // ── Backup ────────────────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Backup"));
        stack.Children.Add(Ui.Hint("Export your menus, app profiles, settings, and custom themes to one file; import it on another PC or after a reinstall."));
        var backupRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        var exportBtn = new Button { Content = "Export settings…" };
        var importBtn = new Button { Content = "Import settings…" };
        var backupStatus = Ui.Hint("");
        exportBtn.Click += async (_, _) =>
        {
            try
            {
                var path = await Pickers.PickSaveFileAsync($"AeroDial-settings-{DateTime.Now:yyyy-MM-dd}", "AeroDial settings", ".aerodial.json");
                if (path is null) return;
                await App.Config.ExportBundleAsync(path);
                backupStatus.Text = $"Exported to {path}";
            }
            catch (Exception ex) { Logger.Error("Export failed", ex); backupStatus.Text = "Export failed: " + ex.Message; }
        };
        importBtn.Click += async (_, _) =>
        {
            try
            {
                var path = await Pickers.PickFileAsync(".json");
                if (path is null) return;
                var dlg = new ContentDialog
                {
                    Title             = "Import settings",
                    Content           = "This replaces your menus and app profiles with the ones in the file " +
                                        "(a backup of the current config is kept as config.json.bak). " +
                                        "Also apply the trigger, appearance, and behavior settings from the file?",
                    PrimaryButtonText   = "Import everything",
                    SecondaryButtonText = "Menus and profiles only",
                    CloseButtonText     = "Cancel",
                    XamlRoot            = XamlRoot,
                };
                var r = await dlg.ShowAsync();
                if (r == ContentDialogResult.None) return;
                int n = await App.Config.ImportBundleAsync(path, includeSettings: r == ContentDialogResult.Primary);
                backupStatus.Text = $"Imported {n} menu(s). Reopen Settings to see the updated pages.";
            }
            catch (Exception ex) { Logger.Error("Import failed", ex); backupStatus.Text = "Import failed: " + ex.Message; }
        };
        backupRow.Children.Add(exportBtn);
        backupRow.Children.Add(importBtn);
        stack.Children.Add(backupRow);
        stack.Children.Add(backupStatus);

        // ── Developer ─────────────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Developer"));
        _debugLog = new ToggleSwitch
        {
            Header     = "Verbose debug logging",
            IsOn       = App.Config.Current.Behavior.EnableDebugLogging,
            OnContent  = "On: writes DEBUG entries to %AppData%\\AeroDial\\aerodial.log",
            OffContent = "Off",
        };
        stack.Children.Add(_debugLog);

        // ── Save ──────────────────────────────────────────────────────────
        var saveBtn = PageKit.SaveButton(); _saved = PageKit.SavedBadge();
        saveBtn.Click += Save;
        var saveRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 14, 0, 0) };
        saveRow.Children.Add(saveBtn);
        saveRow.Children.Add(_saved);
        stack.Children.Add(saveRow);

        _thinning.Toggled   += (_, _) => ScheduleSave();
        _partialArc.Toggled += (_, _) => ScheduleSave();
        _debugLog.Toggled   += (_, _) => ScheduleSave();

        scroll.Content = stack;
        Content = scroll;
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        await App.Config.UpdateAsync(cfg =>
        {
            cfg.Appearance.DynamicRingThinning = _thinning.IsOn;
            cfg.Appearance.PartialArcSubMenu   = _partialArc.IsOn;
            cfg.Behavior.EnableDebugLogging    = _debugLog.IsOn;
        });
        Logger.SetDebugMode(App.Config.Current.Behavior.EnableDebugLogging);
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
    }

    private void ScheduleSave()
    {
        _saveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Stop();
        _saveTimer.Tick -= OnTick;
        _saveTimer.Tick += OnTick;
        _saveTimer.Start();
    }

    private void OnTick(object? s, object e) { _saveTimer!.Stop(); Save(null!, null!); }
}
