using AeroDial.Themes;
using SkiaSharp;

namespace AeroDial.Tests;

public class AccentThemeBuilderTests
{
    [Fact]
    public void Built_theme_uses_the_accent_and_valid_hex_everywhere()
    {
        var t = AccentThemeBuilder.Build(new SKColor(0x00, 0x78, 0xD4));
        Assert.Equal(AccentThemeBuilder.ThemeName, t.Name);
        Assert.Equal("#FF0078D4", t.AccentColor);

        foreach (var hex in new[]
        {
            t.SliceFill, t.SliceFillHover, t.SliceStroke, t.SliceStrokeHover,
            t.SliceGradientInner, t.SliceGradientOuter, t.SliceGradientInnerHover, t.SliceGradientOuterHover,
            t.GlowColor, t.CenterFill, t.CenterStroke, t.RingBorderColor, t.BreadcrumbFill,
        })
        {
            Assert.Matches("^#[0-9A-F]{8}$", hex);
            Assert.NotEqual(SKColors.White, t.ToSKColor(hex)); // White is the parse-failure fallback
        }
    }

    [Fact]
    public void Surfaces_are_darker_than_the_accent_and_ordered_inner_to_outer()
    {
        var accent = new SKColor(0xE0, 0x40, 0x40);
        var t = AccentThemeBuilder.Build(accent);
        float L(string hex) { var c = t.ToSKColor(hex); return c.Red + c.Green + c.Blue; }
        Assert.True(L(t.SliceGradientInner) < L(t.SliceGradientOuter));
        Assert.True(L(t.SliceGradientOuter) < L(t.SliceGradientOuterHover));
        Assert.True(L(t.SliceGradientOuterHover) < accent.Red + accent.Green + accent.Blue);
    }

    [Fact]
    public void Lerp_interpolates_and_clamps()
    {
        var a = new SKColor(0, 0, 0); var b = new SKColor(200, 100, 50);
        Assert.Equal(new SKColor(100, 50, 25, 255), AccentThemeBuilder.Lerp(a, b, 0.5f));
        Assert.Equal(new SKColor(200, 100, 50, 255), AccentThemeBuilder.Lerp(a, b, 5f));
    }
}
