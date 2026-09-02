// AeroDial — DynamicPage.cs
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
// DynamicPage — Scroll-wheel bindings and dynamic content settings
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class DynamicPage : Page
{
    private readonly List<ComboBox> _upBoxes   = [];
    private readonly List<ComboBox> _downBoxes = [];
    private TextBlock               _saved     = null!;
    private string?                 _menuId;
    private DispatcherTimer?        _saveTimer;

    private static readonly string[] ActionNames =
        ["None", "Volume Up", "Volume Down", "Play / Pause", "Next", "Previous", "Mute"];

    private static AeroDial.Config.MediaActionType? IndexToAction(int i) => i switch
    {
        1 => AeroDial.Config.MediaActionType.VolumeUp,
        2 => AeroDial.Config.MediaActionType.VolumeDown,
        3 => AeroDial.Config.MediaActionType.PlayPause,
        4 => AeroDial.Config.MediaActionType.Next,
        5 => AeroDial.Config.MediaActionType.Previous,
        6 => AeroDial.Config.MediaActionType.Mute,
        _ => null,
    };

    private static int ActionToIndex(AeroDial.Config.MediaActionType? a) => a switch
    {
        AeroDial.Config.MediaActionType.VolumeUp   => 1,
        AeroDial.Config.MediaActionType.VolumeDown => 2,
        AeroDial.Config.MediaActionType.PlayPause  => 3,
        AeroDial.Config.MediaActionType.Next       => 4,
        AeroDial.Config.MediaActionType.Previous   => 5,
        AeroDial.Config.MediaActionType.Mute       => 6,
        _                                           => 0,
    };

    public DynamicPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };

        stack.Children.Add(PageKit.PageHeader("Dynamic Content"));
        stack.Children.Add(PageKit.InfoCard(
            "Scroll Wheel Bindings: while the dial is open and your cursor is on a slice, " +
            "scrolling the mouse wheel triggers the assigned action."));

        // Menu picker
        stack.Children.Add(PageKit.SubHeader("Menu"));
        var menuPicker = new ComboBox { Width = 300 };
        foreach (var m in App.Config.Current.Menus)
            menuPicker.Items.Add(new ComboBoxItem { Content = m.Name, Tag = m.Id });
        menuPicker.SelectedIndex = 0;
        stack.Children.Add(menuPicker);

        // Bindings panel — rebuilt when menu selection changes
        var bindingsPanel = new StackPanel { Spacing = 0, Margin = new Thickness(0, 8, 0, 0) };
        stack.Children.Add(bindingsPanel);

        if (App.Config.Current.Menus.Count > 0)
        {
            _menuId = App.Config.Current.Menus[0].Id;
            BuildBindings(bindingsPanel, App.Config.Current.Menus[0]);
        }

        menuPicker.SelectionChanged += (_, _) =>
        {
            if (menuPicker.SelectedItem is ComboBoxItem li && li.Tag is string id)
            {
                _menuId = id;
                var menu = App.Config.Current.Menus.FirstOrDefault(m => m.Id == id);
                _upBoxes.Clear(); _downBoxes.Clear();
                BuildBindings(bindingsPanel, menu);
            }
        };

        _saved = PageKit.SavedBadge();
        _saved.Margin = new Thickness(0, 8, 0, 0);
        stack.Children.Add(_saved);

        scroll.Content = stack; Content = scroll;
    }

    private void BuildBindings(StackPanel panel, AeroDial.Config.RadialMenuConfig? menu)
    {
        panel.Children.Clear();
        _upBoxes.Clear(); _downBoxes.Clear();
        if (menu is null || menu.Items.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "This menu has no items.",
                FontSize = 13,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(130, 200, 200, 220)),
            });
            return;
        }

        // Column headers
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });

        void AddHeader(string text, int col)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 11,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(140, 200, 200, 220)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(col == 0 ? 0 : 4, 0, 0, 0),
            };
            Grid.SetColumn(tb, col); headerGrid.Children.Add(tb);
        }
        AddHeader("Slice", 0); AddHeader("Scroll Up →", 1); AddHeader("Scroll Down →", 2);
        panel.Children.Add(headerGrid);

        foreach (var item in menu.Items)
        {
            var rowGrid = new Grid
            {
                Margin = new Thickness(0, 3, 0, 3),
            };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });

            var label = new TextBlock
            {
                Text = item.Label, FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(label, 0); rowGrid.Children.Add(label);

            var upBox = new ComboBox
            {
                Width = 145, ItemsSource = ActionNames,
                SelectedIndex = ActionToIndex(item.ScrollUpAction),
                Margin = new Thickness(4, 0, 0, 0),
            };
            Grid.SetColumn(upBox, 1); rowGrid.Children.Add(upBox);
            _upBoxes.Add(upBox);
            upBox.SelectionChanged += (_, _) => ScheduleSave();

            var downBox = new ComboBox
            {
                Width = 145, ItemsSource = ActionNames,
                SelectedIndex = ActionToIndex(item.ScrollDownAction),
                Margin = new Thickness(4, 0, 0, 0),
            };
            Grid.SetColumn(downBox, 2); rowGrid.Children.Add(downBox);
            _downBoxes.Add(downBox);
            downBox.SelectionChanged += (_, _) => ScheduleSave();

            panel.Children.Add(rowGrid);
        }
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        var menuId = _menuId;
        var ups    = _upBoxes.Select(b => b.SelectedIndex).ToList();
        var downs  = _downBoxes.Select(b => b.SelectedIndex).ToList();

        await App.Config.UpdateAsync(cfg =>
        {
            var menu = cfg.Menus.FirstOrDefault(m => m.Id == menuId);
            if (menu is null) return;
            for (int i = 0; i < menu.Items.Count && i < ups.Count; i++)
            {
                menu.Items[i].ScrollUpAction   = IndexToAction(ups[i]);
                menu.Items[i].ScrollDownAction = IndexToAction(downs[i]);
            }
        });
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
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
