using AeroDial.Config;
using AeroDial.Core;

namespace AeroDial.Tests;

public class FluentGlyphsTests
{
    [Fact]
    public void Every_legacy_alias_points_at_a_named_glyph()
    {
        var names = FluentGlyphs.Named.Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (legacy, target) in FluentGlyphs.LegacyAliases)
            Assert.True(names.Contains(target), $"alias '{legacy}' -> '{target}' is not a named glyph");
    }

    [Fact]
    public void Named_glyphs_are_unique_and_in_the_private_use_area()
    {
        Assert.Equal(FluentGlyphs.Named.Length, FluentGlyphs.Named.Select(g => g.Name.ToLowerInvariant()).Distinct().Count());
        foreach (var g in FluentGlyphs.Named)
            Assert.InRange(g.Codepoint, 0xE000, 0xF8FF);
    }

    [Theory]
    [InlineData("fluent:play", 0xE768)]
    [InlineData("FLUENT:Play", 0xE768)]
    [InlineData("play", 0xE768)]            // legacy name
    [InlineData("vol_up", 0xE995)]
    [InlineData("fluent:E8B7", 0xE8B7)]     // raw codepoint
    [InlineData("fluent:0xe8b7", 0xE8B7)]
    public void TryResolve_handles_names_aliases_and_codepoints(string key, int expected)
    {
        Assert.True(FluentGlyphs.TryResolve(key, out int cp));
        Assert.Equal(expected, cp);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C:\\app.exe")]
    [InlineData("fluent:")]
    [InlineData("fluent:nope")]
    [InlineData("fluent:0041")]  // outside the PUA
    public void TryResolve_rejects_non_glyph_keys(string? key)
        => Assert.False(FluentGlyphs.TryResolve(key, out _));

    [Fact]
    public void Canonicalize_rewrites_only_legacy_names()
    {
        Assert.Equal("fluent:previous", FluentGlyphs.Canonicalize("prev"));
        Assert.Equal("fluent:play", FluentGlyphs.Canonicalize("fluent:play"));
        Assert.Equal("C:\\x.png", FluentGlyphs.Canonicalize("C:\\x.png"));
    }

    [Fact]
    public void Search_matches_names_and_keywords()
    {
        Assert.Contains(FluentGlyphs.Search("speaker"), g => g.Name == "volume_up");
        Assert.Contains(FluentGlyphs.Search("FOLDER"), g => g.Name == "folder_open");
        Assert.Equal(FluentGlyphs.Named.Length, FluentGlyphs.Search("  ").Count());
    }

    [Fact]
    public void Every_editable_action_has_a_resolvable_default_icon()
    {
        foreach (var a in ActionCatalog.Editable)
            Assert.True(FluentGlyphs.TryResolve(a.DefaultIcon, out _), $"{a.Type}: {a.DefaultIcon}");
    }
}
