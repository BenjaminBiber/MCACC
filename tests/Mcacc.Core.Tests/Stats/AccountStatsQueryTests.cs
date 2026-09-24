using System.Globalization;
using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public class AccountStatsQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo PlusTwo =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static TokenBucket Bucket(
        string utcHour,
        string model = "claude-opus-4-5",
        string project = @"C:\A",
        string session = "s1",
        long input = 100,
        int messages = 1) =>
        new(
            DateTimeOffset.Parse(utcHour, CultureInfo.InvariantCulture),
            model,
            project,
            session,
            new TokenCounts(input, 0, 0, 0, 0),
            messages);

    private static AccountStats Compute(params TokenBucket[] buckets) =>
        AccountStatsQuery.Compute(buckets, Now, 7, PlusTwo);

    [Fact]
    public void The_range_is_the_last_n_local_days_including_today()
    {
        var stats = Compute();

        stats.From.ShouldBe(new DateOnly(2026, 9, 18));
        stats.To.ShouldBe(new DateOnly(2026, 9, 24));
        stats.Days.Select(day => day.Day).ShouldBe(Enumerable.Range(18, 7).Select(day => new DateOnly(2026, 9, day)));
        stats.Days.ShouldAllBe(day => day.Total == 0);
    }

    [Fact]
    public void A_late_utc_hour_belongs_to_the_next_local_day()
    {
        var stats = Compute(Bucket("2026-09-19T21:00Z", input: 1), Bucket("2026-09-19T22:00Z", input: 2));

        stats.Days.Single(day => day.Day == new DateOnly(2026, 9, 19)).Total.ShouldBe(1);
        stats.Days.Single(day => day.Day == new DateOnly(2026, 9, 20)).Total.ShouldBe(2);
    }

    [Fact]
    public void Buckets_outside_the_range_are_left_out()
    {
        var stats = Compute(Bucket("2026-09-17T21:00Z", input: 1), Bucket("2026-09-17T22:00Z", input: 2));

        stats.Tokens.Total.ShouldBe(2);
    }

    [Fact]
    public void Sums_tokens_messages_sessions_and_active_days()
    {
        var stats = Compute(
            Bucket("2026-09-20T08:00Z", session: "s1", messages: 2),
            Bucket("2026-09-20T09:00Z", session: "s2", messages: 3),
            Bucket("2026-09-22T09:00Z", session: "s2", messages: 1));

        stats.Tokens.ShouldBe(new TokenCounts(300, 0, 0, 0, 0));
        stats.Messages.ShouldBe(6);
        stats.Sessions.ShouldBe(2);
        stats.ActiveDays.ShouldBe(2);
    }

    [Fact]
    public void A_day_splits_its_tokens_by_model()
    {
        var stats = Compute(
            Bucket("2026-09-20T08:00Z", model: "claude-opus-4-5", input: 100),
            Bucket("2026-09-20T09:00Z", model: "claude-sonnet-4-5", input: 50));

        stats.Days.Single(day => day.Day == new DateOnly(2026, 9, 20)).TokensByModel
            .ShouldBe(new Dictionary<string, long> { ["claude-opus-4-5"] = 100, ["claude-sonnet-4-5"] = 50 }, ignoreOrder: true);
    }

    [Fact]
    public void Models_are_sorted_by_tokens_and_priced()
    {
        var stats = Compute(
            Bucket("2026-09-20T08:00Z", model: "claude-opus-4-5", input: 100),
            Bucket("2026-09-20T09:00Z", model: "claude-sonnet-4-5", input: 300, messages: 4));

        stats.Models.ShouldBe([
            new ModelUsage("claude-sonnet-4-5", new TokenCounts(300, 0, 0, 0, 0), 4, 0.0009m),
            new ModelUsage("claude-opus-4-5", new TokenCounts(100, 0, 0, 0, 0), 1, 0.0005m),
        ]);
        stats.Cost.ShouldBe(0.0014m);
        stats.UnpricedTokens.ShouldBe(0);
    }

    [Fact]
    public void An_unknown_model_adds_no_cost_but_is_counted_as_unpriced()
    {
        var stats = Compute(
            Bucket("2026-09-20T08:00Z", model: "claude-opus-4-5", input: 100),
            Bucket("2026-09-20T09:00Z", model: "claude-opus-9", input: 40));

        stats.Models.Single(model => model.Model == "claude-opus-9").Cost.ShouldBeNull();
        stats.Cost.ShouldBe(0.0005m);
        stats.UnpricedTokens.ShouldBe(40);
    }

    [Fact]
    public void Only_the_top_projects_are_listed()
    {
        var stats = AccountStatsQuery.Compute(
            [
                Bucket("2026-09-20T08:00Z", project: @"C:\A", input: 10),
                Bucket("2026-09-20T08:00Z", project: @"C:\B", input: 30),
                Bucket("2026-09-20T08:00Z", project: @"C:\C", input: 20),
            ],
            Now,
            7,
            PlusTwo,
            topProjects: 2);

        stats.Projects.ShouldBe([
            new ProjectUsage(@"C:\B", 30, 0.00015m),
            new ProjectUsage(@"C:\C", 20, 0.0001m),
        ]);
    }
}
