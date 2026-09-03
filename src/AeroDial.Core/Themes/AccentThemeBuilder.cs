// AeroDial — AccentThemeBuilder.cs
// Derives a complete dial theme from a single accent color (the Windows accent),
// so the "Auto" theme always matches the desktop. Pure color math; the app feeds
// it the live accent from UISettings and rebuilds when Windows changes it.

using SkiaSharp;

namespace AeroDial.Themes;

public static class AccentThemeBuilder
{
    public const string ThemeName = "Auto (Windows accent)";

    public static AeroTheme Build(SKColor accent)
    {
        // Dark, slightly tinted surfaces: mix the accent toward near-black so the ring
        // reads as "dark theme in the accent hue" rather than a solid block of color.
        var ink = new SKColor(0x08, 0x08, 0x12);
        SKColor Mix(float t, byte alpha) => Lerp(accent, ink, t).WithAlpha(alpha);

        return new AeroTheme
        {
            Name        = ThemeName,
            Description = "Follows the Windows accent color.",
            DimColor    = "#55000000",

            SliceFill        = Hex(Mix(0.84f, 0xE6)),
            SliceFillHover   = Hex(Mix(0.72f, 0xE6)),
            SliceStroke      = Hex(Mix(0.70f, 0x28)),
            SliceStrokeHover = Hex(accent.WithAlpha(0xCC)),
            SliceStrokeWidth = 0.8f,

            SliceGradientInner      = Hex(Mix(0.90f, 0xE6)),
            SliceGradientOuter      = Hex(Mix(0.80f, 0xE6)),
            SliceGradientInnerHover = Hex(Mix(0.78f, 0xE6)),
            SliceGradientOuterHover = Hex(Mix(0.62f, 0xE6)),

            GlowColor       = Hex(accent.WithAlpha(0x88)),
            CenterFill      = Hex(Mix(0.92f, 0xE6)),
            CenterStroke    = Hex(Mix(0.70f, 0x28)),
            IconTint        = "#BBFFFFFF",
            IconTintHover   = "#FFFFFFFF",
            IconStrokeScale = 0.25f,   // same icon weight as the other built-in themes
            LabelColor      = "#99FFFFFF",
            LabelColorHover = "#FFFFFFFF",
            LabelFontSize   = 11f,
            BreadcrumbFill  = Hex(Mix(0.92f, 0xCC)),
            BreadcrumbText  = "#99FFFFFF",
            AccentColor     = Hex(accent.WithAlpha(0xFF)),
            RingBorderColor = Hex(Mix(0.70f, 0x28)),
        };
    }

    public static SKColor Lerp(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red   + (b.Red   - a.Red)   * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue  + (b.Blue  - a.Blue)  * t),
            255);
    }

    /// <summary>#AARRGGBB, the format every theme field uses.</summary>
    public static string Hex(SKColor c) => $"#{c.Alpha:X2}{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
}
