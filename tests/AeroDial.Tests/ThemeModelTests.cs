using AeroDial.Themes;

namespace AeroDial.Tests;

public class ThemeModelTests
{
    [Theory]
    [InlineData(1.0f, 1.0f)]
    [InlineData(1.75f, 1.75f)]   // the built-in themes' icon weight
    [InlineData(0f, 0.2f)]       // below the rasterizer's range
    [InlineData(99f, 3f)]        // above it
    public void StrokeScale_clamps_to_the_rasterizer_range(float raw, float expected)
        => Assert.Equal(expected, new AeroTheme { IconStrokeScale = raw }.StrokeScale, 3);

    [Theory]
    [InlineData(1.0f, 1.0f)]
    [InlineData(1.4f, 1.4f)]
    [InlineData(0.1f, 0.5f)]
    [InlineData(5f, 2f)]
    public void SizeScale_clamps_so_icons_stay_inside_their_slice(float raw, float expected)
        => Assert.Equal(expected, new AeroTheme { IconSizeScale = raw }.SizeScale, 3);

    [Fact]
    public void Icon_scales_default_to_one()
    {
        var t = new AeroTheme();
        Assert.Equal(1f, t.StrokeScale, 3);
        Assert.Equal(1f, t.SizeScale, 3);
    }

    [Fact]
    public void Accent_theme_uses_the_same_icon_weight_as_the_built_ins()
        => Assert.Equal(1.75f, AccentThemeBuilder.Build(new SkiaSharp.SKColor(0x33, 0x77, 0xEE)).IconStrokeScale, 3);
}
