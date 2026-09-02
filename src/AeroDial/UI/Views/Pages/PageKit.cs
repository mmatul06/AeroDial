// AeroDial — PageKit.cs
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


// ── Shared helpers ────────────────────────────────────────────────────────────

internal static class PageKit
{
    public static TextBlock PageHeader(string t) => new()
    {
        Text = t, FontSize = 22,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 16),
    };

    public static TextBlock SubHeader(string t) => new()
    {
        Text = t, FontSize = 13,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 130, 120, 200)),
        Margin = new Thickness(0, 12, 0, 4),
    };

    public static Border InfoCard(string t) => new()
    {
        Background   = new SolidColorBrush(ColorHelper.FromArgb(25, 100, 100, 200)),
        CornerRadius = new CornerRadius(8),
        Padding      = new Thickness(14, 10, 14, 10),
        Margin       = new Thickness(0, 0, 0, 8),
        Child        = new TextBlock
        {
            Text = t, FontSize = 13, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(200, 200, 200, 220)),
        }
    };

    public static TextBlock SavedBadge() => new()
    {
        Text = "✓  Saved", FontSize = 13,
        Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 100, 220, 130)),
        Visibility = Visibility.Collapsed,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static Button SaveButton() => new()
    {
        Content = "Save changes",
        Style   = (Style)Application.Current.Resources["AccentButtonStyle"],
    };

    public static Slider MakeSlider(string header, double min, double max, double step, double val)
        => new() { Header = header, Minimum = min, Maximum = max, StepFrequency = step, Value = val, Width = 340 };
}
