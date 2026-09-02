// AeroDial — Ui.cs
// Theme-resource lookups for code-built UI. Every color in the settings window comes
// from the WinUI theme dictionaries (so the window follows the Windows light/dark
// setting and the user's accent color) instead of hard-coded ARGB values.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AeroDial.UI.Views;

internal static class Ui
{
    public static Brush Brush(string key)
        => Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b
            ? b
            : new SolidColorBrush(Colors.Gray);

    // Text
    public static Brush TextPrimary   => Brush("TextFillColorPrimaryBrush");
    public static Brush TextSecondary => Brush("TextFillColorSecondaryBrush");
    public static Brush TextTertiary  => Brush("TextFillColorTertiaryBrush");
    public static Brush AccentText    => Brush("AccentTextFillColorPrimaryBrush");

    // Surfaces
    public static Brush CardBg        => Brush("CardBackgroundFillColorDefaultBrush");
    public static Brush CardBgAlt     => Brush("CardBackgroundFillColorSecondaryBrush");
    public static Brush CardStroke    => Brush("CardStrokeColorDefaultBrush");
    public static Brush Divider       => Brush("DividerStrokeColorDefaultBrush");
    public static Brush SubtleFill    => Brush("SubtleFillColorSecondaryBrush");
    public static Brush Accent        => Brush("AccentFillColorDefaultBrush");

    // Status (use only for actual status text)
    public static Brush Success       => Brush("SystemFillColorSuccessBrush");
    public static Brush Caution       => Brush("SystemFillColorCautionBrush");
    public static Brush Critical      => Brush("SystemFillColorCriticalBrush");

    /// <summary>A Windows 11 settings-style card: neutral fill, hairline border, 8px corners.</summary>
    public static Border Card(UIElement child, Thickness? padding = null) => new()
    {
        Background      = CardBg,
        BorderBrush     = CardStroke,
        BorderThickness = new Thickness(1),
        CornerRadius    = new CornerRadius(8),
        Padding         = padding ?? new Thickness(16, 12, 16, 12),
        Child           = child,
    };

    /// <summary>Small secondary caption text.</summary>
    public static TextBlock Hint(string text, double size = 12) => new()
    {
        Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap,
        Foreground = TextSecondary,
    };

    /// <summary>A FontIcon from the system icon font.</summary>
    public static FontIcon Glyph(string hex, double size = 16) => new()
    {
        Glyph      = char.ConvertFromUtf32(Convert.ToInt32(hex, 16)),
        FontFamily = new FontFamily(Overlay.IconRegistry.GlyphFontFamily),
        FontSize   = size,
    };
}
