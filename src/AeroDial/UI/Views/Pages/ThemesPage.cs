// AeroDial — ThemesPage.cs
// Themes: list on the left (apply / duplicate / delete), live editor on the right.
// Built-in themes are read-only in the editor until duplicated into a user theme.

using AeroDial.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AeroDial.UI.Views.Pages;

public sealed partial class ThemesPage : Page
{
    private StackPanel       _list   = null!;
    private ThemeEditorPanel _editor = null!;
    private string?          _selected;

    public ThemesPage() => Build();

    private void Build()
    {
        var outer = new Grid();
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ── Left: theme list ──────────────────────────────────────────────
        var leftScroll = new ScrollViewer { Padding = new Thickness(32, 24, 12, 24) };
        var left = new StackPanel { Spacing = 6 };
        left.Children.Add(PageKit.PageHeader("Themes"));

        _list = new StackPanel { Spacing = 4 };
        left.Children.Add(_list);

        var folderBtn = new HyperlinkButton { Content = "Open user themes folder", Padding = new Thickness(0), Margin = new Thickness(0, 10, 0, 0) };
        folderBtn.Click += (_, _) =>
        {
            var dir = AppConstants.UserThemesDir;
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
        };
        left.Children.Add(folderBtn);
        left.Children.Add(Ui.Hint("Theme files are JSON (#AARRGGBB colors). Built-in themes can be duplicated and then edited.", 11));

        leftScroll.Content = left;
        Grid.SetColumn(leftScroll, 0);
        outer.Children.Add(leftScroll);

        // ── Right: editor ─────────────────────────────────────────────────
        _editor = new ThemeEditorPanel();
        _editor.Saved += name =>
        {
            _selected = name;
            RebuildList();
        };
        Grid.SetColumn(_editor, 1);
        outer.Children.Add(_editor);

        Content = outer;

        _selected = App.Config.Current.Appearance.ThemeName;
        RebuildList();
        LoadSelected();
    }

    private void RebuildList()
    {
        _list.Children.Clear();
        var active = App.Config.Current.Appearance.ThemeName;

        foreach (var name in App.Themes.AvailableThemes)
        {
            var t = App.Themes.Get(name);
            if (t is null) continue;
            bool isActive   = name == active;
            bool isSelected = name == _selected;
            var  ac         = t.ToSKColor(t.AccentColor);

            var row = new Grid { Padding = new Thickness(10, 8, 10, 8), ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new Border
            {
                Width = 14, Height = 14, CornerRadius = new CornerRadius(7),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(ac.Alpha, ac.Red, ac.Green, ac.Blue)),
            };
            Grid.SetColumn(dot, 0);
            row.Children.Add(dot);

            var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = name, FontSize = 14,
                FontWeight = isSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            });
            var sub = t.IsBuiltIn ? "Built in" : "Custom";
            if (isActive) sub += "  |  Active";
            text.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = Ui.TextSecondary });
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            if (isActive)
            {
                var check = Ui.Glyph("E73E", 14);
                check.Foreground = Ui.AccentText;
                check.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(check, 2);
                row.Children.Add(check);
            }

            var card = new Button
            {
                Content = row,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = isSelected ? Ui.SubtleFill : Ui.CardBg,
                BorderBrush = isSelected ? Ui.Accent : Ui.CardStroke,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Tag = name,
            };
            card.Click += (_, _) => { _selected = name; RebuildList(); LoadSelected(); };
            _list.Children.Add(card);
        }

        // Actions for the selected theme
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        var selTheme = _selected is null ? null : App.Themes.Get(_selected);

        var applyBtn = new Button
        {
            Content = "Apply",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            IsEnabled = selTheme is not null && _selected != active,
        };
        applyBtn.Click += async (_, _) =>
        {
            if (_selected is null) return;
            await App.Config.UpdateAsync(cfg => cfg.Appearance.ThemeName = _selected);
            RebuildList();
        };
        actions.Children.Add(applyBtn);

        var dupBtn = new Button { Content = "Duplicate", IsEnabled = selTheme is not null };
        dupBtn.Click += (_, _) =>
        {
            if (selTheme is null) return;
            var copy = App.Themes.Duplicate(selTheme, UniqueName(selTheme.Name + " copy"));
            _selected = copy.Name;
            RebuildList();
            LoadSelected();
        };
        actions.Children.Add(dupBtn);

        var delBtn = PageKit.DangerButton("Delete");
        delBtn.IsEnabled = selTheme is not null && !selTheme.IsBuiltIn;
        delBtn.Click += async (_, _) =>
        {
            if (selTheme is null || selTheme.IsBuiltIn) return;
            var dlg = new ContentDialog
            {
                Title             = "Delete theme?",
                Content           = $"Remove \"{selTheme.Name}\" permanently from your user themes?",
                PrimaryButtonText = "Delete",
                CloseButtonText   = "Cancel",
                XamlRoot          = XamlRoot,
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            App.Themes.DeleteUserTheme(selTheme.Name);
            if (App.Config.Current.Appearance.ThemeName == selTheme.Name)
                await App.Config.UpdateAsync(cfg => cfg.Appearance.ThemeName = "Obsidian");
            _selected = App.Config.Current.Appearance.ThemeName;
            RebuildList();
            LoadSelected();
        };
        actions.Children.Add(delBtn);
        _list.Children.Add(actions);
    }

    private void LoadSelected()
    {
        var t = _selected is null ? null : App.Themes.Get(_selected);
        if (t is null) return;
        _editor.Load(t);
    }

    private static string UniqueName(string baseName)
    {
        var names = App.Themes.AvailableThemes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!names.Contains(baseName)) return baseName;
        for (int i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!names.Contains(candidate)) return candidate;
        }
    }
}
