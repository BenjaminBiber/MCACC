using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public class CalendarBackfillTests
{
    private static readonly DateOnly Start = new(2026, 8, 1);

    private static DailyUsage Own(int offset, long tokens) =>
        new(Start.AddDays(offset), tokens == 0 ? new Dictionary<string, long>() : new Dictionary<string, long> { ["m"] = tokens });

    private static DailyTokens Claude(int offset, long tokens) => new(Start.AddDays(offset), tokens);

    [Fact]
    public void Without_a_day_in_both_nothing_is_estimated()
    {
        var days = CalendarBackfill.Fill([Own(0, 0), Own(1, 500)], [Claude(0, 10)]);

        days.ShouldAllBe(day => !day.Estimated);
        days.Select(day => day.Tokens).ShouldBe([0L, 500L]);
    }

    [Fact]
    public void Missing_and_partly_deleted_days_are_estimated_by_the_median_ratio()
    {
        var days = CalendarBackfill.Fill(
            [Own(0, 150), Own(1, 300), Own(2, 450), Own(3, 20), Own(4, 0)],
            [Claude(0, 1), Claude(1, 2), Claude(2, 3), Claude(3, 10), Claude(4, 4)]);

        // The ratios are 150, 150, 150 and 2 for the partly deleted day; the median ignores that one.
        days.Select(day => day.Estimated).ShouldBe([false, false, false, true, true]);
        days.Select(day => day.Tokens).ShouldBe([150L, 300L, 450L, 1500L, 600L]);
        days[3].Own.Total.ShouldBe(20);
    }

    [Fact]
    public void A_day_with_at_least_half_of_the_estimate_keeps_its_own_count()
    {
        var days = CalendarBackfill.Fill(
            [Own(0, 100), Own(1, 100), Own(2, 60)],
            [Claude(0, 1), Claude(1, 1), Claude(2, 1)]);

        days[2].Estimated.ShouldBeFalse();
        days[2].Tokens.ShouldBe(60);
    }

    [Fact]
    public void A_day_that_only_lost_an_excluded_project_is_not_estimated()
    {
        // Day 2 keeps 20 of 300 tokens once a project is out; the full count still says its logs are there.
        var days = CalendarBackfill.Fill(
            [Own(0, 100), Own(1, 100), Own(2, 300)],
            [Claude(0, 1), Claude(1, 1), Claude(2, 3)],
            [Own(0, 100), Own(1, 100), Own(2, 20)]);

        days.ShouldAllBe(day => !day.Estimated);
        days.Select(day => day.Tokens).ShouldBe([100L, 100L, 20L]);
        days[2].Own.Total.ShouldBe(20);
    }

    [Fact]
    public void An_estimate_is_scaled_by_the_share_of_the_counted_projects()
    {
        // The counted projects hold 150 of the 200 tokens of the days with logs: three quarters.
        var days = CalendarBackfill.Fill(
            [Own(0, 100), Own(1, 100), Own(2, 0)],
            [Claude(0, 1), Claude(1, 1), Claude(2, 4)],
            [Own(0, 100), Own(1, 50), Own(2, 0)]);

        days[2].Estimated.ShouldBeTrue();
        days[2].Tokens.ShouldBe(300);
    }

    [Fact]
    public void Counted_days_must_match_the_own_ones()
    {
        Should.Throw<ArgumentException>(() => CalendarBackfill.Fill([Own(0, 1), Own(1, 1)], [], [Own(0, 1)]));
    }

    [Fact]
    public void Days_only_in_the_summary_but_outside_the_range_are_left_out()
    {
        var days = CalendarBackfill.Fill([Own(0, 100)], [Claude(0, 1), Claude(-5, 7)]);

        days.ShouldHaveSingleItem().Day.ShouldBe(Start);
    }
}
