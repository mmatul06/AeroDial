using System.Text.Json;
using AeroDial.Themes;

namespace AeroDial.Tests;

/// <summary>
/// Theme files are written and read with one options object. They used to use two that
/// disagreed on casing, so a saved theme came back as an all-default theme named "Custom"
/// and disappeared from the list. These tests pin both directions.
/// </summary>
public class ThemeJsonTests
{
    [Fact]
    public void A_saved_theme_reads_back_unchanged()
    {
        var saved = new AeroTheme
        {
            Name = "Obsidian copy",
            Description = "round trip",
            IconStrokeScale = 0.25f,
            IconSizeScale = 1.4f,
            AccentColor = "#FF7C6EF7",
            LabelFontSize = 13f,
        };

        var json   = JsonSerializer.Serialize(saved, ThemeJson.Options);
        var loaded = JsonSerializer.Deserialize<AeroTheme>(json, ThemeJson.Options)!;

        Assert.Equal("Obsidian copy", loaded.Name);
        Assert.Equal("round trip", loaded.Description);
        Assert.Equal(0.25f, loaded.IconStrokeScale, 3);
        Assert.Equal(1.4f, loaded.IconSizeScale, 3);
        Assert.Equal("#FF7C6EF7", loaded.AccentColor);
        Assert.Equal(13f, loaded.LabelFontSize, 3);
    }

    [Fact]
    public void Themes_are_written_in_camel_case_like_the_bundled_files()
    {
        var json = JsonSerializer.Serialize(new AeroTheme { Name = "X" }, ThemeJson.Options);

        Assert.Contains("\"name\":", json);
        Assert.DoesNotContain("\"Name\":", json);
    }

    [Fact]
    public void Pascal_case_files_written_by_older_builds_still_load()
    {
        const string legacy = """
            {
              "Name": "Custom Sakura",
              "IconStrokeScale": 2.5,
              "AccentColor": "#FFEE88AA"
            }
            """;

        var loaded = JsonSerializer.Deserialize<AeroTheme>(legacy, ThemeJson.Options)!;

        Assert.Equal("Custom Sakura", loaded.Name);   // not the default "Custom"
        Assert.Equal(2.5f, loaded.IconStrokeScale, 3);
        Assert.Equal("#FFEE88AA", loaded.AccentColor);
    }

    [Fact]
    public void Clamped_helpers_are_not_written_to_the_file()
    {
        var json = JsonSerializer.Serialize(new AeroTheme { IconStrokeScale = 9f }, ThemeJson.Options);

        Assert.DoesNotContain("strokeScale", json);
        Assert.DoesNotContain("sizeScale", json);
        Assert.Contains("iconStrokeScale", json);
    }
}
