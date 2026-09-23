using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Model;

public class AccountPaletteTests
{
    [Fact]
    public void Colors_are_the_eight_palette_entries_in_order()
    {
        AccountPalette.Colors.ShouldBe(
            ["#D97757", "#6A9BCC", "#7D9B76", "#9B7BA8", "#C2A477", "#5E8C8A", "#B5667B", "#6B6F76"]);
    }

    [Fact]
    public void NextFreeColor_returns_first_color_when_nothing_is_used()
    {
        AccountPalette.NextFreeColor([]).ShouldBe("#D97757");
    }

    [Fact]
    public void NextFreeColor_skips_used_colors_case_insensitively()
    {
        AccountPalette.NextFreeColor(["#d97757", "#6a9bcc"]).ShouldBe("#7D9B76");
    }

    [Fact]
    public void NextFreeColor_ignores_colors_outside_the_palette()
    {
        AccountPalette.NextFreeColor(["#123456"]).ShouldBe("#D97757");
    }

    [Fact]
    public void NextFreeColor_cycles_to_first_color_when_all_are_used()
    {
        AccountPalette.NextFreeColor(AccountPalette.Colors).ShouldBe("#D97757");
    }

    [Theory]
    [InlineData("#D97757")]
    [InlineData("#abcdef")]
    [InlineData("#000000")]
    public void IsValidColor_accepts_hash_and_six_hex_digits(string color)
    {
        AccountPalette.IsValidColor(color).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("D97757")]
    [InlineData("#D9775")]
    [InlineData("#D977570")]
    [InlineData("#GGGGGG")]
    [InlineData("#D97757\n")]
    [InlineData(" #D97757")]
    [InlineData(null)]
    public void IsValidColor_rejects_everything_else(string? color)
    {
        AccountPalette.IsValidColor(color).ShouldBeFalse();
    }
}
