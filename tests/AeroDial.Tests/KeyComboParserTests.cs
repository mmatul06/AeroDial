using AeroDial.Core;

namespace AeroDial.Tests;

public class KeyComboParserTests
{
    [Theory]
    [InlineData("Ctrl", 0x11)]
    [InlineData("control", 0x11)]
    [InlineData("Shift", 0x10)]
    [InlineData("Alt", 0x12)]
    [InlineData("Win", 0x5B)]
    [InlineData("Enter", 0x0D)]
    [InlineData("return", 0x0D)]
    [InlineData("Space", 0x20)]
    [InlineData("Esc", 0x1B)]
    [InlineData("a", 0x41)]
    [InlineData("Z", 0x5A)]
    [InlineData("5", 0x35)]
    [InlineData("F1", 0x70)]
    [InlineData("F12", 0x7B)]
    [InlineData("F24", 0x87)]
    [InlineData("[", 0xDB)]
    [InlineData("]", 0xDD)]
    [InlineData("PageUp", 0x21)]
    public void TokenToVk_maps_known_tokens(string token, int expected)
        => Assert.Equal((byte)expected, KeyComboParser.TokenToVk(token));

    [Theory]
    [InlineData("")]
    [InlineData("F0")]
    [InlineData("F25")]
    [InlineData("Hyper")]
    [InlineData("ab")]
    public void TokenToVk_returns_zero_for_unknown_tokens(string token)
        => Assert.Equal(0, KeyComboParser.TokenToVk(token));

    [Fact]
    public void Parse_splits_modifiers_from_keys_in_order()
    {
        var chord = KeyComboParser.Parse("Ctrl+Shift+S");
        Assert.Equal(new byte[] { 0x11, 0x10 }, chord.Modifiers);
        Assert.Equal(new byte[] { 0x53 }, chord.Keys);
        Assert.Equal(new byte[] { 0x11, 0x10, 0x53 }, chord.PressOrder.ToArray());
    }

    [Fact]
    public void Parse_keeps_modifiers_first_even_when_written_last()
    {
        var chord = KeyComboParser.Parse("D+Win");
        Assert.Equal(new byte[] { 0x5B, 0x44 }, chord.PressOrder.ToArray());
    }

    [Fact]
    public void Parse_skips_unknown_tokens_but_keeps_the_rest()
    {
        var chord = KeyComboParser.Parse("Ctrl+Bogus+C");
        Assert.False(chord.IsEmpty);
        Assert.Equal(new byte[] { 0x11, 0x43 }, chord.PressOrder.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("Bogus+Nope")]
    public void Parse_reports_empty_when_nothing_is_recognized(string input)
        => Assert.True(KeyComboParser.Parse(input).IsEmpty);

    [Fact]
    public void Parse_tolerates_whitespace_around_tokens()
    {
        var chord = KeyComboParser.Parse(" ctrl + alt + Delete ");
        Assert.Equal(new byte[] { 0x11, 0x12, 0x2E }, chord.PressOrder.ToArray());
    }
}
