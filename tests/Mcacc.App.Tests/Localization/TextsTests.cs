using System.Globalization;
using Mcacc.App.Localization;
using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Localization;

public class TextsTests
{
    private static DateTimeOffset Local(int month, int day, int hour, int minute) =>
        new(new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Local));

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1_234, "1,2 k")]
    [InlineData(12_345, "12,3 k")]
    [InlineData(123_456, "123 k")]
    [InlineData(1_234_567, "1,2 M")]
    [InlineData(2_500_000_000, "2,5 Mrd.")]
    public void Token_amounts_are_short(long tokens, string expected)
    {
        Texts.TokenAmount(tokens).ShouldBe(expected);
    }

    [Theory]
    [InlineData("0", "≈ 0,00 $")]
    [InlineData("0.004", "≈ < 0,01 $")]
    [InlineData("1234.567", "≈ 1.234,57 $")]
    public void Costs_are_marked_as_an_estimate(string cost, string expected)
    {
        Texts.Cost(decimal.Parse(cost, CultureInfo.InvariantCulture)).ShouldBe(expected);
    }

    [Fact]
    public void Percentages_and_shares_use_a_german_comma()
    {
        Texts.Percent(42.4).ShouldBe("42 %");
        Texts.Share(0.125).ShouldBe("12,5 %");
        Texts.Count(12_345).ShouldBe("12.345");
    }

    [Fact]
    public void Amount_tooltips_name_the_share_of_the_total()
    {
        Texts.AmountTooltip("Input", 250, 1_000).ShouldBe("Input: 250 · 25,0 %");
        Texts.AmountTooltip("Input", 0, 0).ShouldBe("Input: 0");
        Texts.ProjectTooltip("/home/ben/shop", 1_500, 0.5m).ShouldBe("/home/ben/shop\n1,5 k Tokens · ≈ 0,50 $");
        Texts.ProjectTooltip("", 1_500, 0.5m).ShouldStartWith(Texts.InfoNoProject);
        Texts.ProjectTooltip("/home/ben/shop", 1_500, null).ShouldBe("/home/ben/shop\n1,5 k Tokens");
        Texts.ProjectTooltip("/home/ben/shop", 1_500, null, 1).ShouldBe("/home/ben/shop\n1,5 k Tokens\ninkl. 1 Worktree");
        Texts.ProjectTooltip("/home/ben/shop", 1_500, null, 3).ShouldEndWith("\ninkl. 3 Worktrees");
    }

    [Theory]
    [InlineData(true, false, null, "Claude Max")]
    [InlineData(false, true, null, "Claude Pro")]
    [InlineData(null, null, "max", "Claude Max")]
    [InlineData(null, null, "team", "Claude Team")]
    [InlineData(null, null, "enterprise", "Claude Enterprise")]
    [InlineData(false, false, "free_trial", "Free trial")]
    [InlineData(null, null, null, null)]
    public void The_plan_comes_from_the_profile_or_the_credentials(
        bool? hasMax,
        bool? hasPro,
        string? subscriptionType,
        string? expected)
    {
        Texts.Plan(hasMax, hasPro, subscriptionType).ShouldBe(expected);
    }

    [Theory]
    [InlineData("seven_day_opus", "Woche Opus")]
    [InlineData("seven_day_oauth_apps", "Woche Oauth apps")]
    [InlineData("monthly", "Monthly")]
    public void Extra_windows_get_a_readable_name(string key, string expected)
    {
        Texts.UsageExtraWindow(key).ShouldBe(expected);
    }

    [Fact]
    public void Dates_are_german_and_local()
    {
        Texts.InfoDate(Local(9, 4, 8, 5)).ShouldBe("04.09.2026");
        Texts.InfoDateTime(Local(9, 4, 8, 5)).ShouldBe("04.09.2026 08:05");
    }

    [Fact]
    public void Since_names_the_day_only_when_it_is_not_today()
    {
        Texts.InfoSince(Local(9, 24, 8, 5), Local(9, 24, 12, 0)).ShouldBe("läuft seit 08:05");
        Texts.InfoSince(Local(9, 22, 8, 5), Local(9, 24, 12, 0)).ShouldBe("läuft seit 22.09. 08:05");
    }

    [Theory]
    [InlineData(@"C:\Users\ben\RiderProjects\MCACC", "MCACC")]
    [InlineData("/home/ben/code/shop/", "shop")]
    [InlineData("", Texts.InfoNoProject)]
    public void A_project_is_named_after_its_folder(string cwd, string expected)
    {
        Texts.ProjectName(cwd).ShouldBe(expected);
    }

    [Theory]
    [InlineData("claude-opus-4-5-20251101", "Opus 4.5")]
    [InlineData("claude-opus-4-6[1m]", "Opus 4.6")]
    [InlineData("claude-fable-5-1", "Fable 5.1")]
    [InlineData("claude-opus-5", "Opus 5")]
    [InlineData("claude-haiku-4-5-20251001", "Haiku 4.5")]
    [InlineData("claude-3-5-sonnet-20241022", "Sonnet 3.5")]
    [InlineData("claude-3-opus-20240229", "Opus 3")]
    [InlineData("<synthetic>", "<synthetic>")]
    public void Model_ids_become_readable_names(string model, string expected)
    {
        Texts.ModelName(model).ShouldBe(expected);
    }

    [Fact]
    public void A_day_tooltip_lists_the_models_largest_first()
    {
        var day = new DailyUsage(
            new DateOnly(2026, 9, 23),
            new Dictionary<string, long> { ["claude-haiku-4-5"] = 2_000, ["claude-opus-4-5"] = 1_500_000 });

        Texts.DayLabel(day.Day).ShouldBe("23.09.");
        Texts.DayTooltip(day).ShouldBe("Mittwoch, 23.09. · 1,5 M Tokens\nOpus 4.5: 1,5 M\nHaiku 4.5: 2 k");
    }

    [Fact]
    public void A_history_tooltip_shows_both_windows()
    {
        Texts.HistoryTooltip(Local(9, 23, 14, 5), 42, null).ShouldBe("23.09. 14:05\n5h: 42 %\nWoche: —");
    }

    [Fact]
    public void Counts_and_progress_read_naturally()
    {
        Texts.InfoDays(30).ShouldBe("30 Tage");
        Texts.InfoActiveDaysOf(12, 30).ShouldBe("12 von 30");
        Texts.InfoScanProgress(0.426).ShouldBe("Logs werden gelesen … 43 %");
        Texts.InfoUnpriced(1_234_567).ShouldBe("1,2 M Tokens ohne bekannten Preis");
    }
}
