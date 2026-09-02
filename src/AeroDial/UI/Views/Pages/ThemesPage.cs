// AeroDial — ThemesPage.cs
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
// ThemesPage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class ThemesPage : Page
{
    public ThemesPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };

        stack.Children.Add(PageKit.PageHeader("Themes"));
        stack.Children.Add(PageKit.InfoCard(
            "To create a custom theme: copy any .json from the themes\\ folder next to AeroDial.exe " +
            "into %AppData%\\Roaming\\AeroDial\\themes\\, rename it, and edit the colour values (#AARRGGBB format)."));

        var current = App.Config.Current.Appearance.ThemeName;

        foreach (var name in App.Themes.AvailableThemes)
        {
            var t = App.Themes.Get(name);
            if (t is null) continue;

            bool active = name == current;
            var ac = t.ToSKColor(t.AccentColor);

            var card = new Border
            {
                Background   = new SolidColorBrush(active
                    ? ColorHelper.FromArgb(45, ac.Red, ac.Green, ac.Blue)
                    : ColorHelper.FromArgb(18, 120, 110, 180)),
                CornerRadius    = new CornerRadius(10),
                Padding         = new Thickness(16, 12, 16, 12),
                Margin          = new Thickness(0, 5, 0, 0),
                BorderThickness = new Thickness(active ? 1.5 : 0.5),
                BorderBrush     = new SolidColorBrush(active
                    ? ColorHelper.FromArgb(180, ac.Red, ac.Green, ac.Blue)
                    : ColorHelper.FromArgb(35, 140, 130, 180)),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Spacing = 3 };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            nameRow.Children.Add(new Border
            {
                Width = 14, Height = 14, CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(
                    ColorHelper.FromArgb(ac.Alpha, ac.Red, ac.Green, ac.Blue)),
            });
            nameRow.Children.Add(new TextBlock
            {
                Text = name, FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            if (active)
                nameRow.Children.Add(new TextBlock
                {
                    Text = "Active", FontSize = 11,
                    Foreground = new SolidColorBrush(
                        ColorHelper.FromArgb(200, ac.Red, ac.Green, ac.Blue)),
                    VerticalAlignment = VerticalAlignment.Center,
                });

            info.Children.Add(nameRow);
            info.Children.Add(new TextBlock
            {
                Text = t.Description, FontSize = 12,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(150, 180, 180, 200)),
            });

            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing     = 6,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (!active)
            {
                var applyBtn = new Button
                {
                    Content = "Apply", VerticalAlignment = VerticalAlignment.Center, Tag = name,
                };
                applyBtn.Click += async (sender, _) =>
                {
                    if (sender is Button b && b.Tag is string n)
                    {
                        await App.Config.UpdateAsync(cfg => cfg.Appearance.ThemeName = n);
                        Build();
                    }
                };
                actionPanel.Children.Add(applyBtn);
            }

            if (!t.IsBuiltIn)
            {
                var deleteBtn = new Button
                {
                    Content      = "Delete",
                    Tag          = name,
                    Background   = new SolidColorBrush(ColorHelper.FromArgb(180, 180, 50, 50)),
                    Foreground   = new SolidColorBrush(Colors.White),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                deleteBtn.Click += async (sender, _) =>
                {
                    if (sender is not Button b || b.Tag is not string n) return;
                    var dlg = new ContentDialog
                    {
                        Title             = "Delete theme?",
                        Content           = $"Remove \"{n}\" permanently from your user themes?",
                        PrimaryButtonText = "Delete",
                        CloseButtonText   = "Cancel",
                        XamlRoot          = b.XamlRoot,
                    };
                    if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
                    App.Themes.DeleteUserTheme(n);
                    // Fall back to Obsidian if the active theme was deleted
                    if (App.Config.Current.Appearance.ThemeName == n)
                        await App.Config.UpdateAsync(cfg => cfg.Appearance.ThemeName = "Obsidian");
                    Build();
                };
                actionPanel.Children.Add(deleteBtn);
            }

            if (actionPanel.Children.Count > 0)
            {
                Grid.SetColumn(actionPanel, 1);
                grid.Children.Add(actionPanel);
            }

            card.Child = grid;
            stack.Children.Add(card);
        }

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        var openUserThemes = new Button { Content = "Open user themes folder" };
        openUserThemes.Click += (_, _) =>
        {
            var dir = AppConstants.UserThemesDir;
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
        };
        var openBuiltinThemes = new Button { Content = "Open built-in themes folder" };
        openBuiltinThemes.Click += (_, _) =>
        {
            var dir = AppConstants.ThemesDir;
            if (Directory.Exists(dir))
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
        };
        btnRow.Children.Add(openUserThemes);
        btnRow.Children.Add(openBuiltinThemes);
        stack.Children.Add(btnRow);

        scroll.Content = stack; Content = scroll;
    }
}
