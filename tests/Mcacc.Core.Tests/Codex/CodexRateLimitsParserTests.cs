using Mcacc.Core.Codex;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Codex;

public class CodexRateLimitsParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    private static string RolloutLine(int index) =>
        FixtureFiles.ReadAllText("codex-rollout.jsonl").Split('\n')[index];

    [Fact]
    public void The_endpoint_reply_maps_both_windows()
    {
        var limits = CodexRateLimitsParser
            .ParseLive(FixtureFiles.ReadAllText("codex-usage-live.json"), Now)
            .ShouldNotBeNull();

        limits.At.ShouldBe(Now);
        limits.FiveHour.ShouldNotBeNull().Utilization.ShouldBe(29.5);
        limits.FiveHour.ShouldNotBeNull().ResetsAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1791291393));
        limits.SevenDay.ShouldNotBeNull().Utilization.ShouldBe(5);
        limits.SevenDay.ShouldNotBeNull().ResetsAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1791878193));
        limits.PlanType.ShouldBe("plus");
        limits.Extra.ShouldBeNull();
    }

    [Fact]
    public void A_session_line_maps_both_windows_and_carries_its_own_timestamp()
    {
        var limits = CodexRateLimitsParser.ParseRolloutLine(RolloutLine(2)).ShouldNotBeNull();

        limits.FiveHour.ShouldNotBeNull().Utilization.ShouldBe(42.0);
        limits.SevenDay.ShouldNotBeNull().Utilization.ShouldBe(7.5);
        limits.At.ShouldBe(new DateTimeOffset(2026, 10, 1, 7, 15, 42, 250, TimeSpan.Zero));
        limits.PlanType.ShouldBe("plus");
    }

    /// <summary>
    /// The two spellings of the same windows: the endpoint counts in seconds, the session files in minutes. The
    /// mapping must come out the same either way.
    /// </summary>
    [Fact]
    public void Both_spellings_of_the_window_length_land_in_the_same_windows()
    {
        var live = CodexRateLimitsParser
            .ParseLive(FixtureFiles.ReadAllText("codex-usage-live.json"), Now)
            .ShouldNotBeNull();
        var file = CodexRateLimitsParser.ParseRolloutLine(RolloutLine(2)).ShouldNotBeNull();

        live.FiveHour.ShouldNotBeNull();
        live.SevenDay.ShouldNotBeNull();
        file.FiveHour.ShouldNotBeNull();
        file.SevenDay.ShouldNotBeNull();
    }

    /// <summary>The length decides, not the position — a swapped reply must not swap the bars.</summary>
    [Fact]
    public void The_window_length_decides_not_the_position()
    {
        const string swapped = """
            {
              "rate_limit": {
                "primary_window":   { "used_percent": 7, "limit_window_seconds": 604800, "reset_at": 1 },
                "secondary_window": { "used_percent": 3, "limit_window_seconds": 18000,  "reset_at": 2 }
              }
            }
            """;

        var limits = CodexRateLimitsParser.ParseLive(swapped, Now).ShouldNotBeNull();

        limits.FiveHour.ShouldNotBeNull().Utilization.ShouldBe(3);
        limits.SevenDay.ShouldNotBeNull().Utilization.ShouldBe(7);
    }

    /// <summary>Without a length the position is all there is, so it decides.</summary>
    [Fact]
    public void Without_a_window_length_the_position_decides()
    {
        const string unnamed = """
            {
              "rate_limit": {
                "primary_window":   { "used_percent": 11, "reset_at": 1 },
                "secondary_window": { "used_percent": 22, "reset_at": 2 }
              }
            }
            """;

        var limits = CodexRateLimitsParser.ParseLive(unnamed, Now).ShouldNotBeNull();

        limits.FiveHour.ShouldNotBeNull().Utilization.ShouldBe(11);
        limits.SevenDay.ShouldNotBeNull().Utilization.ShouldBe(22);
    }

    [Fact]
    public void A_window_of_an_unknown_length_goes_to_the_extras_instead_of_a_main_bar()
    {
        const string extra = """
            {
              "rate_limit": {
                "primary_window":   { "used_percent": 4, "limit_window_seconds": 18000, "reset_at": 1 },
                "secondary_window": { "used_percent": 9, "limit_window_seconds": 86400, "reset_at": 2 }
              }
            }
            """;

        var limits = CodexRateLimitsParser.ParseLive(extra, Now).ShouldNotBeNull();

        limits.FiveHour.ShouldNotBeNull().Utilization.ShouldBe(4);
        limits.SevenDay.ShouldBeNull();
        limits.Extra.ShouldNotBeNull().Keys.ShouldBe(["window_1440m"]);
    }

    [Fact]
    public void Credits_keep_the_balance_as_the_reply_spells_it()
    {
        var credits = CodexRateLimitsParser.ParseRolloutLine(RolloutLine(2)).ShouldNotBeNull().Credits.ShouldNotBeNull();

        credits.HasCredits.ShouldBeTrue();
        credits.Unlimited.ShouldBeFalse();
        credits.Balance.ShouldBe("12.50");
    }

    [Fact]
    public void A_line_without_rate_limits_yields_nothing()
    {
        CodexRateLimitsParser.ParseRolloutLine(RolloutLine(0)).ShouldBeNull();
        CodexRateLimitsParser.ParseRolloutLine(RolloutLine(3)).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{ "rate_limit": {} }""")]
    [InlineData("""{ "payload": { "rate_limits": {} } }""")]
    public void Garbage_and_empty_limits_yield_nothing_without_throwing(string json)
    {
        CodexRateLimitsParser.ParseLive(json, Now).ShouldBeNull();
        CodexRateLimitsParser.ParseRolloutLine(json).ShouldBeNull();
    }

    /// <summary>A reset time far outside what a date can hold must not take the whole read down.</summary>
    [Fact]
    public void An_impossible_reset_time_leaves_the_window_without_one()
    {
        const string absurd = """
            {
              "rate_limit": { "primary_window": { "used_percent": 1, "limit_window_seconds": 18000, "reset_at": 999999999999999 } }
            }
            """;

        var window = CodexRateLimitsParser.ParseLive(absurd, Now).ShouldNotBeNull().FiveHour.ShouldNotBeNull();

        window.Utilization.ShouldBe(1);
        window.ResetsAt.ShouldBeNull();
    }
}
