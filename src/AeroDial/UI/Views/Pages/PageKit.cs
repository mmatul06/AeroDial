// AeroDial — PageKit.cs
// Shared builders for the settings pages. Colors come from Ui (theme resources).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AeroDial.UI.Views.Pages;

internal static class PageKit
{
    public static TextBlock PageHeader(string t) => new()
    {
        Text = t, FontSize = 26,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 16),
    };

    public static TextBlock SubHeader(string t) => new()
    {
        Text = t, FontSize = 14,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 14, 0, 6),
    };

    /// <summary>Neutral explanatory card (Windows 11 settings style).</summary>
    public static Border InfoCard(string t)
    {
        var card = Ui.Card(new TextBlock
        {
            Text = t, FontSize = 13, TextWrapping = TextWrapping.Wrap,
            Foreground = Ui.TextSecondary,
        });
        card.Margin = new Thickness(0, 0, 0, 8);
        return card;
    }

    public static TextBlock SavedBadge() => new()
    {
        Text = "Saved", FontSize = 13,
        Foreground = Ui.Success,
        Visibility = Visibility.Collapsed,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static Button SaveButton() => new()
    {
        Content = "Save changes",
        Style   = (Style)Application.Current.Resources["AccentButtonStyle"],
    };

    /// <summary>A button whose text is drawn in the critical color (delete, remove, quit).</summary>
    public static Button DangerButton(string content)
        => new() { Content = content, Foreground = Ui.Critical };

    /// <summary>A slider that fills its column. Never give it a fixed Width: a fixed-width
    /// control with Stretch alignment is centered in its slot and drifts when the window resizes.</summary>
    public static Slider MakeSlider(string header, double min, double max, double step, double val)
        => new()
        {
            Header = header, Minimum = min, Maximum = max, StepFrequency = step, Value = val,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8),
        };
}
