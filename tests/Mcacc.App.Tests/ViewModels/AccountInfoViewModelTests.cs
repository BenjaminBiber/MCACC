using Avalonia.Input;
using Mcacc.App.Controls;
using Mcacc.App.Localization;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Stats;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

public sealed class AccountInfoViewModelTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TestServices _services = new(OsKind.Windows, Standard, Privat);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _services.Dispose();

    private AccountInfoViewModel Page() => new(_services.Services, Privat);

    private static string? Field(AccountInfoViewModel page, string label) =>
        page.Fields.FirstOrDefault(field => field.Label == label)?.Value;

    private static AccountUsage Usage(double? fiveHour, double? sevenDay, IReadOnlyDictionary<string, UsageWindow>? extra = null) =>
        new(UsageState.Ok, new UsageSnapshot(
            new UsageWindow(fiveHour, DateTimeOffset.Now.AddHours(2)),
            new UsageWindow(sevenDay, null),
            DateTimeOffset.Now,
            extra));

    private static UsageHistoryPoint Point(double hoursAgo, double? fiveHour, double? sevenDay) =>
        new(DateTimeOffset.Now.AddHours(-hoursAgo), fiveHour, null, sevenDay, null);

    private static TokenBucket Bucket(string model, long input, string session = "s1", string project = "/code/shop") =>
        new(DateTimeOffset.Now.AddHours(-1), model, project, session, new TokenCounts(input, 0, 0, 0, 0), Messages: 1);

    private static AccountProfile Profile(string email) => new(
        "uuid-1",
        email,
        "Ben Biber",
        "Ben",
        HasClaudeMax: true,
        HasClaudePro: false,
        new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero),
        "Bens Org",
        "claude_max",
        "stripe_subscription",
        "default_claude_max_20x");

    // Limits

    [Fact]
    public void The_page_shows_the_cached_limits_as_rings()
    {
        _services.Usage.Cached["privat"] = Usage(42, 94);

        var page = Page();

        page.Rings.Select(ring => ring.Label).ShouldBe([Texts.UsageFiveHour, Texts.UsageSevenDay]);
        page.Rings.Select(ring => ring.ValueText).ShouldBe(["42 %", "94 %"]);
        page.Rings[0].Slices[0].Color.ShouldBe(ChartColor.Accent);
        page.Rings[1].Slices[0].Color.ShouldBe(ChartColor.Danger);
        page.Rings[0].ResetText.ShouldNotBeNull();
        page.LimitsAsOf.ShouldNotBeNull();
        page.HasRings.ShouldBeTrue();
    }

    [Fact]
    public void Extra_windows_follow_the_two_main_rings_by_name()
    {
        _services.Usage.Cached["privat"] = Usage(1, 2, new Dictionary<string, UsageWindow>
        {
            ["seven_day_sonnet"] = new(3, null),
            ["seven_day_opus"] = new(4, null),
        });

        Page().Rings.Select(ring => ring.Label).ShouldBe(["5h", "Woche", "Woche Opus", "Woche Sonnet"]);
    }

    [Fact]
    public void A_window_without_a_value_gets_no_ring_and_no_snapshot_gets_none()
    {
        _services.Usage.Cached["privat"] = Usage(42, null);
        Page().Rings.Select(ring => ring.Label).ShouldBe([Texts.UsageFiveHour]);

        _services.Usage.Cached.Remove("privat");
        var empty = Page();
        empty.Rings.ShouldBeEmpty();
        empty.HasRings.ShouldBeFalse();
        empty.LimitsAsOf.ShouldBeNull();
    }

    [Fact]
    public async Task Activating_refreshes_the_limits()
    {
        _services.Usage.Cached["privat"] = Usage(42, 7);
        _services.Usage.Fresh["privat"] = Usage(55, 8);
        var page = Page();

        await page.ActivateAsync(Ct);

        page.Rings[0].ValueText.ShouldBe("55 %");
    }

    [Fact]
    public void A_token_of_another_account_shows_the_hint()
    {
        _services.Usage.Cached["privat"] = new AccountUsage(UsageState.WrongAccount, null, "other@example.com");

        Page().LimitsHint.ShouldBe(Texts.UsageWrongAccount("other@example.com"));
    }

    [Theory]
    [InlineData(UsageState.RateLimited)]
    [InlineData(UsageState.Unauthorized)]
    [InlineData(UsageState.Error)]
    public void A_failed_refresh_says_the_limits_are_stale_instead_of_repeating_their_time(UsageState state)
    {
        _services.Usage.Cached["privat"] = Usage(42, 7) with { State = state };

        var page = Page();

        page.LimitsHint.ShouldBe(Texts.InfoLimitsStale);
        page.LimitsAsOf.ShouldNotBeNull();
    }

    [Fact]
    public void A_failed_refresh_without_limits_shows_no_hint()
    {
        _services.Usage.Cached["privat"] = new AccountUsage(UsageState.RateLimited, null);

        Page().LimitsHint.ShouldBeNull();
    }

    [Fact]
    public async Task A_poll_of_this_account_updates_the_rings()
    {
        _services.Usage.Cached["privat"] = Usage(42, 7);
        var page = Page();
        await page.ActivateAsync(Ct);
        _services.Usage.Fresh["privat"] = Usage(77, 7);

        await _services.Poller.TickAsync(Ct);

        page.Rings[0].ValueText.ShouldBe("77 %");
    }

    [Fact]
    public async Task A_left_page_ignores_polls()
    {
        _services.Usage.Cached["privat"] = Usage(42, 7);
        var page = Page();
        await page.ActivateAsync(Ct);
        page.Deactivate();
        _services.Usage.Fresh["privat"] = Usage(77, 7);

        await _services.Poller.TickAsync(Ct);

        page.Rings[0].ValueText.ShouldBe("42 %");
    }

    // History

    [Fact]
    public void The_limits_card_shows_the_rings_first_and_switches_to_the_history()
    {
        var page = Page();
        page.IsLimitsHistory.ShouldBeFalse();

        page.ShowLimitsViewCommand.Execute("history");
        page.IsLimitsHistory.ShouldBeTrue();

        page.ShowLimitsViewCommand.Execute("now");
        page.IsLimitsHistory.ShouldBeFalse();
    }

    [Fact]
    public void The_history_shows_the_last_24_hours_first()
    {
        _services.Usage.History["privat"] = [Point(30, 80, 20), Point(2, 40, 10)];

        var page = Page();

        page.IsHistoryDay.ShouldBeTrue();
        page.HistoryMax.ShouldBe(24);
        page.HistoryGap.ShouldBe(AccountInfoViewModel.HistoryDayGap);
        page.HistoryTicks.ShouldNotBeEmpty();
        page.HistorySeries.Select(series => series.Name).ShouldBe([Texts.UsageFiveHour, Texts.UsageSevenDay]);
        var point = page.HistorySeries[0].Points.ShouldHaveSingleItem();
        point.X.ShouldBe(22, tolerance: 0.01);
        point.Y.ShouldBe(40);
        point.Tooltip.ShouldNotBeNull();
    }

    [Fact]
    public void The_week_view_shows_seven_days()
    {
        _services.Usage.History["privat"] = [Point(30, 80, 20), Point(2, 40, 10)];
        var page = Page();

        page.ShowHistoryCommand.Execute("7d");

        page.IsHistoryWeek.ShouldBeTrue();
        page.IsHistoryDay.ShouldBeFalse();
        page.HistoryMax.ShouldBe(168);
        page.HistoryGap.ShouldBe(AccountInfoViewModel.HistoryWeekGap);
        page.HistoryTicks.Count.ShouldBeInRange(7, 8);
        page.HistorySeries[0].Points.Select(p => p.Y).ShouldBe([80, 40]);
    }

    [Fact]
    public void The_day_view_marks_every_third_clock_hour_and_midnight_by_its_date()
    {
        var start = new DateTimeOffset(2026, 9, 23, 14, 20, 0, TimeSpan.Zero);

        var ticks = AccountInfoViewModel.TicksFor(start, TimeSpan.FromHours(24), week: false, TimeZoneInfo.Utc);

        ticks.Select(tick => tick.Label)
            .ShouldBe(["15:00", "18:00", "21:00", "24.09.", "03:00", "06:00", "09:00", "12:00"]);
        ticks[0].X.ShouldBe(40 / 60d, tolerance: 1e-9);
        ticks[^1].X.ShouldBe(21 + 40 / 60d, tolerance: 1e-9);
    }

    [Fact]
    public void The_week_view_marks_each_midnight_with_the_weekday()
    {
        var start = new DateTimeOffset(2026, 9, 17, 14, 20, 0, TimeSpan.Zero);

        var ticks = AccountInfoViewModel.TicksFor(start, TimeSpan.FromDays(7), week: true, TimeZoneInfo.Utc);

        ticks.Select(tick => tick.Label)
            .ShouldBe(["Fr 18.09.", "Sa 19.09.", "So 20.09.", "Mo 21.09.", "Di 22.09.", "Mi 23.09.", "Do 24.09."]);
        ticks[0].X.ShouldBe(9 + 40 / 60d, tolerance: 1e-9);
    }

    [Fact]
    public void A_point_without_a_value_is_left_out_of_that_line_only()
    {
        _services.Usage.History["privat"] = [Point(1, null, 5)];

        var page = Page();

        page.HistorySeries[0].Points.ShouldBeEmpty();
        page.HistorySeries[1].Points.ShouldHaveSingleItem();
    }

    [Fact]
    public void Without_history_there_is_no_series()
    {
        Page().HistorySeries.ShouldBeEmpty();
    }

    // Account

    [Fact]
    public void The_profile_wins_over_the_login_file()
    {
        _services.SetLogin(Privat, "login@example.com");
        _services.Usage.Profiles["privat"] = Profile("profile@example.com");

        var page = Page();

        Field(page, Texts.InfoEmail).ShouldBe("profile@example.com");
        Field(page, Texts.InfoName).ShouldBe("Ben Biber");
        Field(page, Texts.InfoPlan).ShouldBe("Claude Max");
        Field(page, Texts.InfoOrganization).ShouldBe("Bens Org");
        Field(page, Texts.InfoBilling).ShouldBe("Stripe subscription");
        Field(page, Texts.InfoRateLimitTier).ShouldBe("Default claude max 20x");
        Field(page, Texts.InfoCreatedAt).ShouldBe(Texts.InfoDate(new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero)));
        page.HasAccountData.ShouldBeTrue();
    }

    [Fact]
    public void Without_a_profile_the_login_file_fills_the_fields()
    {
        _services.Login.ByClaudeDir[_services.Services.Resolver.Resolve(Privat).EffectiveClaudeDir] =
            new ClaudeLoginInfo("login@example.com", "Firma", "Ben", "uuid-1", "admin", 12, "native", 3);

        var page = Page();

        Field(page, Texts.InfoEmail).ShouldBe("login@example.com");
        Field(page, Texts.InfoName).ShouldBe("Ben");
        Field(page, Texts.InfoOrganization).ShouldBe("Firma");
        Field(page, Texts.InfoRole).ShouldBe("Admin");
        Field(page, Texts.InfoStartups).ShouldBe("12");
        Field(page, Texts.InfoInstallMethod).ShouldBe("Native");
        Field(page, Texts.InfoProjects).ShouldBe("3");
        Field(page, Texts.InfoPlan).ShouldBeNull();
    }

    [Fact]
    public async Task The_credentials_add_plan_scopes_and_expiry_but_never_the_token()
    {
        var expires = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        _services.Credentials.Results["privat"] = new CredentialReadResult(
            CredentialReadStatus.Found,
            new OAuthCredentials("sk-ant-secret", expires, "max", ["user:inference", "user:profile"]));
        var page = Page();

        await page.ActivateAsync(Ct);

        Field(page, Texts.InfoPlan).ShouldBe("Claude Max");
        Field(page, Texts.InfoScopes).ShouldBe("user:inference, user:profile");
        Field(page, Texts.InfoTokenExpires).ShouldBe(Texts.InfoDateTime(expires));
        page.Fields.ShouldAllBe(field => !field.Value.Contains("sk-ant"));
    }

    [Fact]
    public async Task A_running_desktop_shows_since_when()
    {
        _services.Detector.Instances.Add(_services.RunningInstance(Privat) with { StartTime = DateTimeOffset.Now.AddMinutes(-5) });
        var page = Page();

        await page.ActivateAsync(Ct);

        Field(page, Texts.InfoDesktop).ShouldNotBeNull().ShouldStartWith("läuft seit");
    }

    [Fact]
    public async Task Without_any_data_the_page_says_so_and_names_only_the_desktop()
    {
        var page = Page();
        page.Fields.ShouldBeEmpty();

        await page.ActivateAsync(Ct);

        page.HasAccountData.ShouldBeFalse();
        page.Fields.ShouldHaveSingleItem().ShouldBe(new InfoFieldViewModel(Texts.InfoDesktop, Texts.InfoDesktopNotRunning));
    }

    // Claude Code usage

    [Fact]
    public async Task The_statistics_sum_up_the_logs()
    {
        _services.Stats.Buckets.AddRange([Bucket("claude-opus-4-5", 1_000_000), Bucket("claude-haiku-4-5", 2_000, "s2")]);
        var page = Page();

        await page.ActivateAsync(Ct);

        page.HasSessions.ShouldBeTrue();
        page.IsScanning.ShouldBeFalse();
        page.Kpis.Select(kpi => kpi.Label)
            .ShouldBe([Texts.InfoTokens, Texts.InfoCost, Texts.InfoSessions, Texts.InfoMessages, Texts.InfoActiveDays]);
        page.Kpis.Select(kpi => kpi.Value).ShouldBe(["1 M", "≈ 5,00 $", "2", "2", "1 von 30"]);
        page.Kpis[1].Caption.ShouldBe(Texts.InfoCostNote);
        page.DailyBars.Count.ShouldBe(30);
        page.Models.Select(model => model.Name).ShouldBe(["Opus 4.5", "Haiku 4.5"]);
        page.Models[0].Cost.ShouldBe("≈ 5,00 $");
        page.Projects.ShouldHaveSingleItem().Label.ShouldBe("shop");
        page.UnpricedText.ShouldBeNull();
    }

    [Fact]
    public async Task An_excluded_project_counts_nowhere()
    {
        _services.Stats.Buckets.AddRange(
        [
            Bucket("claude-opus-4-5", 1_000_000),
            Bucket("claude-haiku-4-5", 2_000, "s2", project: "/code/lab"),
        ]);
        _services.Accounts.UpdateSettings(settings => settings with { ExcludedProjects = ["/CODE/LAB/"] });
        var page = Page();

        await page.ActivateAsync(Ct);

        page.Kpis.Select(kpi => kpi.Value).ShouldBe(["1 M", "≈ 5,00 $", "1", "1", "1 von 30"]);
        page.Models.ShouldHaveSingleItem().Name.ShouldBe("Opus 4.5");
        page.Projects.ShouldHaveSingleItem().Label.ShouldBe("shop");
        page.CalendarDays.Sum(day => day.Value).ShouldBe(1_000_000);
        page.ExcludedNote.ShouldBe(Texts.InfoExcludedProjects(1));
    }

    [Fact]
    public async Task An_excluded_project_of_another_account_is_not_mentioned()
    {
        _services.Stats.Buckets.Add(Bucket("claude-opus-4-5", 1_000));
        _services.Accounts.UpdateSettings(settings => settings with { ExcludedProjects = ["/code/elsewhere"] });
        var page = Page();

        await page.ActivateAsync(Ct);

        page.ExcludedNote.ShouldBeNull();
        page.Kpis[0].Value.ShouldBe(Texts.TokenAmount(1_000));
    }

    [Fact]
    public async Task Back_from_the_settings_the_page_counts_with_the_new_list()
    {
        _services.Stats.Buckets.AddRange([Bucket("claude-opus-4-5", 1_000), Bucket("claude-opus-4-5", 2_000, project: "/code/lab")]);
        var page = Page();
        await page.ActivateAsync(Ct);
        page.Kpis[0].Value.ShouldBe(Texts.TokenAmount(3_000));
        _services.Stats.Gate = new TaskCompletionSource();

        _services.Accounts.UpdateSettings(settings => settings with { ExcludedProjects = ["/code/lab"] });
        var again = page.ActivateAsync(Ct);

        // Before the logs are read again.
        page.Kpis[0].Value.ShouldBe(Texts.TokenAmount(1_000));
        page.ExcludedNote.ShouldBe(Texts.InfoExcludedProjects(1));
        _services.Stats.Gate.SetResult();
        await again;
    }

    [Fact]
    public async Task The_token_kinds_make_the_donut()
    {
        _services.Stats.Buckets.Add(new TokenBucket(
            DateTimeOffset.Now.AddHours(-1), "claude-opus-4-5", "/p", "s1", new TokenCounts(100, 200, 30, 20, 650), 1));
        var page = Page();

        await page.ActivateAsync(Ct);

        page.KindSlices.Select(slice => slice.Value).ShouldBe([100, 200, 50, 650]);
        page.KindSlices.Select(slice => slice.Label)
            .ShouldBe([Texts.InfoKindInput, Texts.InfoKindOutput, Texts.InfoKindCacheWrite, Texts.InfoKindCacheRead]);
        page.KindCenterText.ShouldBe("1 k");
        page.KindLegend.Select(item => item.Value).ShouldBe(["100", "200", "50", "650"]);
    }

    [Fact]
    public async Task The_period_switch_recomputes_the_statistics()
    {
        _services.Stats.Buckets.Add(Bucket("claude-opus-4-5", 1_000));
        var page = Page();
        await page.ActivateAsync(Ct);

        page.ShowDaysCommand.Execute("7");

        page.UsageDays.ShouldBe(7);
        page.IsDays7.ShouldBeTrue();
        page.IsDays30.ShouldBeFalse();
        page.DailyBars.Count.ShouldBe(7);
        page.Kpis[4].Value.ShouldBe("1 von 7");
    }

    [Fact]
    public async Task The_calendar_covers_the_last_year_whatever_the_period()
    {
        _services.Stats.Buckets.Add(Bucket("claude-opus-4-5", 1_000));
        var page = Page();
        await page.ActivateAsync(Ct);
        page.IsUsageCalendar.ShouldBeFalse();

        page.ShowUsageViewCommand.Execute("calendar");
        page.ShowDaysCommand.Execute("7");

        page.IsUsageCalendar.ShouldBeTrue();
        page.CalendarDays.Count.ShouldBe(AccountInfoViewModel.CalendarSpan);
        page.CalendarDays[^1].Day.ShouldBe(DateOnly.FromDateTime(DateTime.Now));
        page.CalendarDays.Sum(day => day.Value).ShouldBe(1_000);
        page.CalendarDays.ShouldAllBe(day => day.Tooltip != null);

        page.ShowUsageViewCommand.Execute("bars");
        page.IsUsageCalendar.ShouldBeFalse();
    }

    [Fact]
    public async Task The_calendar_estimates_days_without_logs_from_the_claude_summary()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var gone = today.AddDays(-40);
        _services.Stats.Buckets.Add(Bucket("claude-opus-4-5", 1_000));
        _services.Stats.Summary.AddRange([new ClaudeDailyTokens(gone, 3), new ClaudeDailyTokens(today, 10)]);
        var page = Page();

        await page.ActivateAsync(Ct);

        // Today both know it: 1 000 by MCACC's count against 10 by Claude's, a factor of 100.
        var day = page.CalendarDays.Single(entry => entry.Day == gone);
        day.Value.ShouldBe(300);
        day.Estimated.ShouldBeTrue();
        page.CalendarDays.Single(entry => entry.Day == today).Estimated.ShouldBeFalse();
        day.Tooltip.ShouldBe(Texts.EstimatedDayTooltip(gone, 300));
        page.CalendarNote.ShouldBe(Texts.InfoCalendarEstimated(1));
        page.Kpis[0].Value.ShouldBe(Texts.TokenAmount(1_000));
    }

    [Fact]
    public async Task Without_estimated_days_the_calendar_has_no_note()
    {
        _services.Stats.Buckets.Add(Bucket("claude-opus-4-5", 1_000));
        var page = Page();

        await page.ActivateAsync(Ct);

        page.CalendarNote.ShouldBeNull();
    }

    [Fact]
    public async Task The_five_largest_models_get_their_own_color_and_the_rest_is_other()
    {
        _services.Stats.Buckets.AddRange(Enumerable.Range(1, 7).Select(i => Bucket($"claude-test-{i}", 1_000 * (8 - i))));
        var page = Page();

        await page.ActivateAsync(Ct);

        page.ModelLegend.Select(item => item.Color).ShouldBe(
            [ChartColor.Series1, ChartColor.Series2, ChartColor.Series3, ChartColor.Series4, ChartColor.Series5, ChartColor.Series6]);
        page.ModelLegend[^1].Label.ShouldBe(Texts.InfoOtherModels);
        page.Models.Count.ShouldBe(7);
        page.Models[^1].Color.ShouldBe(ChartColor.Series6);
        page.DailyBars.Single(bar => bar.Total > 0).Segments.Count.ShouldBe(6);
        page.UnpricedText.ShouldBe(Texts.InfoUnpriced(28_000));
    }

    [Fact]
    public async Task Without_a_priced_model_the_cost_is_left_out()
    {
        _services.Stats.Buckets.Add(Bucket("claude-test-1", 1_000));
        var page = Page();

        await page.ActivateAsync(Ct);

        page.Kpis.Select(kpi => kpi.Label)
            .ShouldBe([Texts.InfoTokens, Texts.InfoSessions, Texts.InfoMessages, Texts.InfoActiveDays]);
        page.Projects.ShouldHaveSingleItem().Tooltip.ShouldBe("/code/shop\n1 k Tokens");
        page.Models.ShouldHaveSingleItem().Cost.ShouldBe(Texts.InfoNoValue);
        page.UnpricedText.ShouldBe(Texts.InfoUnpriced(1_000));
    }

    [Fact]
    public async Task No_sessions_in_the_period_shows_the_empty_state()
    {
        var page = Page();

        await page.ActivateAsync(Ct);

        page.HasSessions.ShouldBeFalse();
        page.StatsError.ShouldBeNull();
    }

    [Fact]
    public async Task The_page_shows_that_it_is_scanning()
    {
        _services.Stats.Gate = new TaskCompletionSource();
        var page = Page();

        var activation = page.ActivateAsync(Ct);

        page.IsScanning.ShouldBeTrue();
        page.ScanText.ShouldBe(Texts.InfoScanning);
        _services.Stats.Gate.SetResult();
        await activation;
        page.IsScanning.ShouldBeFalse();
    }

    [Fact]
    public async Task A_failing_scan_shows_an_error()
    {
        _services.Stats.ThrowOnLoad = new IOException("locked");
        var page = Page();

        await page.ActivateAsync(Ct);

        page.StatsError.ShouldBe(Texts.InfoScanFailed);
        page.IsScanning.ShouldBeFalse();
    }

    [Fact]
    public async Task Leaving_the_page_stops_the_updates_but_the_scan_runs_to_completion()
    {
        _services.Stats.Gate = new TaskCompletionSource();
        var page = Page();
        var activation = page.ActivateAsync(Ct);
        var kpis = page.Kpis;

        page.Deactivate();
        _services.Stats.Buckets.Add(Bucket("claude-opus-4-5", 1_000));
        _services.Stats.Gate.SetResult();
        await activation;

        _services.Stats.LoadToken.IsCancellationRequested.ShouldBeFalse();
        _services.Stats.Completed.ShouldBe(1);
        page.HasSessions.ShouldBeFalse();
        page.Kpis.ShouldBeSameAs(kpis);
        page.StatsError.ShouldBeNull();
    }

    [Fact]
    public async Task A_scan_that_fails_after_the_page_was_left_shows_no_error()
    {
        _services.Stats.Gate = new TaskCompletionSource();
        _services.Stats.ThrowOnLoad = new IOException("locked");
        var page = Page();
        var activation = page.ActivateAsync(Ct);

        page.Deactivate();
        _services.Stats.Gate.SetResult();
        await activation;

        page.StatsError.ShouldBeNull();
    }

    // Keys

    [Fact]
    public void F5_refreshes_the_limits()
    {
        var page = Page();

        page.OnKeyDown(Key.F5, KeyModifiers.None).ShouldBeTrue();

        _services.Usage.Refreshed.ShouldContain("privat");
    }

    [Theory]
    [InlineData(Key.Escape, KeyModifiers.None)]
    [InlineData(Key.Back, KeyModifiers.None)]
    [InlineData(Key.Left, KeyModifiers.Alt)]
    public void Escape_backspace_and_alt_left_go_back(Key key, KeyModifiers modifiers)
    {
        var picker = new AccountPickerViewModel(_services.Services);
        var page = Page();
        _services.Navigator.Reset(picker);
        _services.Navigator.NavigateTo(page);

        page.OnKeyDown(key, modifiers).ShouldBeTrue();

        _services.Navigator.CurrentPage.ShouldBe(picker);
    }
}
