// AeroDial — ProfilesPage.cs
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
// ProfilesPage — per-app menu profiles (context-aware dial)
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class ProfilesPage : Page
{
    // Working copy — not written to config until Save.
    private List<AppProfileConfig> _profiles = [];
    private StackPanel _rows  = null!;
    private TextBlock   _saved = null!;

    public ProfilesPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 8 };

        stack.Children.Add(PageKit.PageHeader("App Profiles"));
        stack.Children.Add(PageKit.InfoCard(
            "Assign a menu to an app. When that app is in the foreground and you open the dial, " +
            "it shows the assigned menu instead of the default. Apps are matched by process name. " +
            "Choose Disabled to keep the dial out of an app entirely (the trigger button passes through)."));

        _profiles = App.Config.Current.AppProfiles
            .Select(p => new AppProfileConfig { ProcessName = p.ProcessName, MenuId = p.MenuId })
            .ToList();

        _rows = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 4) };
        stack.Children.Add(_rows);
        RebuildRows();

        var addBtn = new Button { Content = "Add profile" };
        addBtn.Click += (_, _) =>
        {
            _profiles.Add(new AppProfileConfig { ProcessName = "", MenuId = DefaultMenuId() });
            RebuildRows();
        };

        var detectBtn    = new Button { Content = "Add from running app" };
        var detectFlyout = new MenuFlyout();
        detectBtn.Flyout = detectFlyout;
        detectFlyout.Opening += (_, _) => PopulateRunningApps(detectFlyout);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        btnRow.Children.Add(addBtn);
        btnRow.Children.Add(detectBtn);
        stack.Children.Add(btnRow);

        var saveBtn = PageKit.SaveButton();
        saveBtn.Margin = new Thickness(0, 16, 0, 0);
        saveBtn.Click += Save;
        _saved = PageKit.SavedBadge();
        _saved.Margin = new Thickness(0, 16, 0, 0);
        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        saveRow.Children.Add(saveBtn);
        saveRow.Children.Add(_saved);
        stack.Children.Add(saveRow);

        scroll.Content = stack;
        Content = scroll;
    }

    private static string DefaultMenuId()
        => App.Config.Current.Menus.FirstOrDefault()?.Id ?? "default";

    private void RebuildRows()
    {
        _rows.Children.Clear();

        if (_profiles.Count == 0)
        {
            _rows.Children.Add(new TextBlock
            {
                Text       = "No app profiles yet. Add one below.",
                FontSize   = 13,
                Foreground = Ui.TextSecondary,
            });
            return;
        }

        var menus = App.Config.Current.Menus;
        foreach (var prof in _profiles)
        {
            var row = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                Spacing           = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var nameBox = new TextBox
            {
                Text            = prof.ProcessName,
                PlaceholderText = "process name (e.g. acad)",
                Width           = 220,
            };
            nameBox.TextChanged += (_, _) => prof.ProcessName = nameBox.Text.Trim();

            var arrow = new TextBlock { Text = "→", VerticalAlignment = VerticalAlignment.Center, FontSize = 15 };

            var combo = new ComboBox { Width = 220 };
            int selected = -1;
            for (int m = 0; m < menus.Count; m++)
            {
                combo.Items.Add(new ComboBoxItem { Content = menus[m].Name, Tag = menus[m].Id });
                if (menus[m].Id == prof.MenuId) selected = m;
            }
            // Reserved target: the dial does not open for this app and the trigger passes through.
            var disabledItem = new ComboBoxItem { Content = "Disabled (dial does not open)", Tag = ProfileMatcher.DisabledMenuId };
            ToolTipService.SetToolTip(disabledItem, "Useful for games or apps that use the trigger button themselves.");
            combo.Items.Add(disabledItem);
            if (prof.MenuId == ProfileMatcher.DisabledMenuId) selected = combo.Items.Count - 1;

            combo.SelectedIndex = selected >= 0 ? selected : 0;
            if (selected < 0 && menus.Count > 0) prof.MenuId = menus[0].Id; // fell back — keep model in sync
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is ComboBoxItem ci && ci.Tag is string id) prof.MenuId = id;
            };

            var del = new Button { Content = "✕", Width = 40 };
            del.Click += (_, _) => { _profiles.Remove(prof); RebuildRows(); };

            row.Children.Add(nameBox);
            row.Children.Add(arrow);
            row.Children.Add(combo);
            row.Children.Add(del);
            _rows.Children.Add(row);
        }
    }

    private void PopulateRunningApps(MenuFlyout flyout)
    {
        flyout.Items.Clear();
        var names = GetRunningAppProcessNames();
        if (names.Count == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem { Text = "No running apps found", IsEnabled = false });
            return;
        }
        foreach (var name in names)
        {
            var mi = new MenuFlyoutItem { Text = name };
            mi.Click += (_, _) =>
            {
                if (!_profiles.Any(p => string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase)))
                    _profiles.Add(new AppProfileConfig { ProcessName = name, MenuId = DefaultMenuId() });
                RebuildRows();
            };
            flyout.Items.Add(mi);
        }
    }

    // Running processes that own a visible top-level window, by process name.
    private static List<string> GetRunningAppProcessNames()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                    names.Add(p.ProcessName);
            }
            catch { /* protected/elevated process — skip */ }
            finally { p.Dispose(); }
        }
        names.Remove(AppConstants.AppName); // don't offer AeroDial itself
        return names.ToList();
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        var cleaned = _profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.ProcessName))
            .Select(p => new AppProfileConfig { ProcessName = p.ProcessName.Trim(), MenuId = p.MenuId })
            .ToList();

        await App.Config.UpdateAsync(cfg => cfg.AppProfiles = cleaned);
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
    }
}
