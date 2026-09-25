using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public class UsageGapEstimateTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour, int minute) => Day.AddHours(hour).AddMinutes(minute);

    private static UsageValue V(int hour, int minute, double value) => new(At(hour, minute), value);

    [Fact]
    public void A_reset_in_the_gap_holds_the_value_drops_to_zero_and_rises_from_the_start_of_the_next_window()
    {
        // The history of 2026-09-24 in UTC: 97 % at 10:08, the window reset at 10:40, the next one began at 11:10.
        var line = UsageGapEstimate.Bridge(
            V(10, 8, 97), At(10, 40),
            V(11, 15, 5), At(16, 10),
            UsageGapEstimate.FiveHour);

        line.ShouldBe([V(10, 8, 97), V(10, 40, 97), V(10, 40, 0), V(11, 10, 0), V(11, 15, 5)]);
    }

    [Fact]
    public void Without_a_reset_in_the_gap_the_line_is_straight()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 20), At(12, 0), V(10, 0, 35), At(12, 0), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 20), V(10, 0, 35)]);
    }

    [Fact]
    public void Without_the_next_reset_the_line_rises_right_from_the_reset()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 60), At(9, 0), V(11, 0, 10), null, UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 60), V(9, 0, 60), V(9, 0, 0), V(11, 0, 10)]);
    }

    [Fact]
    public void Without_an_active_window_the_value_holds_until_the_next_window_begins()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 0), null, V(12, 0, 4), At(16, 30), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 0), V(11, 30, 0), V(12, 0, 4)]);
    }

    [Fact]
    public void Without_any_reset_the_line_is_straight()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 0), null, V(12, 0, 4), null, UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 0), V(12, 0, 4)]);
    }

    [Fact]
    public void A_next_window_that_seems_to_begin_before_the_reset_begins_at_the_reset()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 50), At(9, 0), V(10, 0, 5), At(13, 0), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 50), V(9, 0, 50), V(9, 0, 0), V(10, 0, 5)]);
    }

    [Fact]
    public void A_next_window_that_seems_to_begin_after_the_next_point_begins_there()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 50), At(9, 0), V(10, 0, 5), At(16, 0), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 50), V(9, 0, 50), V(9, 0, 0), V(10, 0, 0), V(10, 0, 5)]);
    }

    [Fact]
    public void A_zero_before_the_reset_leaves_no_empty_corners()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 0), At(9, 0), V(12, 0, 3), At(16, 30), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 0), V(9, 0, 0), V(11, 30, 0), V(12, 0, 3)]);
    }

    [Fact]
    public void The_week_window_is_seven_days_long()
    {
        // The next week began at 07:00 the day after and resets seven days later; with five hours it would begin after b.
        var begin = At(7, 0).AddDays(1);
        var b = new UsageValue(At(8, 0).AddDays(1), 2);

        var line = UsageGapEstimate.Bridge(V(15, 0, 38), At(16, 0), b, begin.AddDays(7), UsageGapEstimate.SevenDay);

        line.ShouldBe([V(15, 0, 38), V(16, 0, 38), V(16, 0, 0), new UsageValue(begin, 0), b]);
    }

    [Fact]
    public void From_cuts_the_line_at_the_start_and_interpolates_the_crossing()
    {
        UsageValue[] line = [V(8, 0, 40), V(10, 0, 60), V(12, 0, 0)];

        UsageGapEstimate.From(line, At(9, 0)).ShouldBe([V(9, 0, 50), V(10, 0, 60), V(12, 0, 0)]);
    }

    [Fact]
    public void From_keeps_a_line_that_begins_after_the_start()
    {
        UsageValue[] line = [V(10, 0, 60), V(12, 0, 0)];

        UsageGapEstimate.From(line, At(9, 0)).ShouldBe(line);
    }

    [Fact]
    public void From_drops_a_line_that_ends_before_the_start()
    {
        UsageValue[] line = [V(6, 0, 60), V(8, 0, 0)];

        UsageGapEstimate.From(line, At(9, 0)).ShouldBeEmpty();
    }

    [Fact]
    public void From_passes_a_vertical_edge_before_the_start_by_its_lower_end()
    {
        UsageValue[] line = [V(6, 0, 60), V(8, 0, 60), V(8, 0, 0), V(12, 0, 0), V(13, 0, 8)];

        UsageGapEstimate.From(line, At(9, 0)).ShouldBe([V(9, 0, 0), V(12, 0, 0), V(13, 0, 8)]);
    }
}
