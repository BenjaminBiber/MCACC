using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public class UsageResponseParserTests
{
    private static readonly DateTimeOffset FetchedAt = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reads_both_windows_with_utilization_and_reset()
    {
        var snapshot = UsageResponseParser.Parse(FixtureFiles.ReadAllText("usage-ok.json"), FetchedAt);

        snapshot.ShouldNotBeNull();
        snapshot.FetchedAt.ShouldBe(FetchedAt);
        snapshot.FiveHour.ShouldBe(new UsageWindow(42.5, new DateTimeOffset(2026, 9, 23, 14, 30, 0, TimeSpan.Zero)));
        snapshot.SevenDay.ShouldBe(new UsageWindow(7, new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.FromHours(2))));
    }

    [Fact]
    public void A_null_window_and_null_fields_stay_unknown()
    {
        var snapshot = UsageResponseParser.Parse(FixtureFiles.ReadAllText("usage-nulls.json"), FetchedAt);

        snapshot.ShouldNotBeNull();
        snapshot.FiveHour.ShouldBe(new UsageWindow(null, null));
        snapshot.SevenDay.ShouldBeNull();
    }

    [Fact]
    public void Missing_windows_stay_null()
    {
        var snapshot = UsageResponseParser.Parse("""{ "something_else": 1 }""", FetchedAt);

        snapshot.ShouldBe(new UsageSnapshot(null, null, FetchedAt));
    }

    [Fact]
    public void Unknown_fields_inside_a_window_are_ignored()
    {
        var json = """{ "five_hour": { "utilization": 12, "resets_at": "2026-09-23T14:30:00Z", "remaining": 4 } }""";

        UsageResponseParser.Parse(json, FetchedAt)!.FiveHour.ShouldBe(
            new UsageWindow(12, new DateTimeOffset(2026, 9, 23, 14, 30, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData("""{ "five_hour": { "utilization": "42", "resets_at": "nope" } }""")]
    [InlineData("""{ "five_hour": 42 }""")]
    public void Wrongly_typed_fields_stay_unknown(string json)
    {
        UsageResponseParser.Parse(json, FetchedAt)!.FiveHour?.Utilization.ShouldBeNull();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Anything_that_is_not_a_json_object_parses_as_null(string json)
    {
        UsageResponseParser.Parse(json, FetchedAt).ShouldBeNull();
    }

    [Fact]
    public void Reads_the_other_seven_day_windows_as_extra()
    {
        var json = """
            {
              "five_hour": { "utilization": 1, "resets_at": null },
              "seven_day": { "utilization": 2, "resets_at": null },
              "seven_day_opus": { "utilization": 55.5, "resets_at": "2026-09-27T09:00:00Z" },
              "seven_day_sonnet": { "utilization": null, "resets_at": null },
              "seven_day_oauth_apps": null,
              "extra_usage": { "utilization": 99 }
            }
            """;

        var extra = UsageResponseParser.Parse(json, FetchedAt)!.Extra;

        extra.ShouldNotBeNull();
        extra.Keys.Order().ShouldBe(["seven_day_opus", "seven_day_sonnet"]);
        extra["seven_day_opus"].ShouldBe(new UsageWindow(55.5, new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero)));
        extra["seven_day_sonnet"].ShouldBe(new UsageWindow(null, null));
    }

    [Fact]
    public void Without_extra_windows_extra_stays_null()
    {
        UsageResponseParser.Parse(FixtureFiles.ReadAllText("usage-ok.json"), FetchedAt)!.Extra.ShouldBeNull();
    }
}
