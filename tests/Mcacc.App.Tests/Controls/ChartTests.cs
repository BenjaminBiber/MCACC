using Mcacc.App.Controls;
using Mcacc.App.Theme;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Controls;

public class ChartTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(0.7, 1)]
    [InlineData(1, 1)]
    [InlineData(1.2, 2)]
    [InlineData(2.2, 2.5)]
    [InlineData(3, 5)]
    [InlineData(7, 10)]
    [InlineData(100, 100)]
    [InlineData(101, 200)]
    [InlineData(1_234_567, 2_000_000)]
    public void The_axis_ends_at_the_next_nice_number(double value, double expected)
    {
        ChartScale.NiceMax(value).ShouldBe(expected, tolerance: 1e-9);
    }

    [Fact]
    public void Ticks_split_the_axis_evenly()
    {
        ChartScale.Ticks(100).ShouldBe([0, 25, 50, 75, 100]);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(50, 50)]
    [InlineData(100, 0)]
    [InlineData(150, 0)]
    [InlineData(-10, 100)]
    public void Map_turns_a_value_into_a_clamped_pixel(double value, double expected)
    {
        ChartScale.Map(value, 0, 100, 100, 0).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(99, 0)]
    [InlineData(100, 1)]
    [InlineData(399, 3)]
    [InlineData(400, -1)]
    [InlineData(-1, -1)]
    public void BandAt_finds_the_band_under_the_pointer(double x, int expected)
    {
        ChartScale.BandAt(x, 400, 4).ShouldBe(expected);
    }

    [Fact]
    public void A_stack_totals_its_segments()
    {
        new BarStack("a", [new BarSegment(2, ChartColor.Series1), new BarSegment(3, ChartColor.Series2)]).Total.ShouldBe(5);
    }

    [Theory]
    [InlineData(ChartColor.Series1, ThemeKeys.Series1)]
    [InlineData(ChartColor.Series6, ThemeKeys.Series6)]
    [InlineData(ChartColor.Accent, ThemeKeys.Accent)]
    [InlineData(ChartColor.Danger, ThemeKeys.Danger)]
    [InlineData(ChartColor.Muted, ThemeKeys.TextTertiary)]
    [InlineData(ChartColor.Track, ThemeKeys.Border)]
    public void Each_chart_color_names_a_theme_token(ChartColor color, string key)
    {
        ChartControl.KeyOf(color).ShouldBe(key);
    }
}
