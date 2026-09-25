using Mcacc.App.Services;
using Mcacc.App.Tests.TestSupport;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Services;

public class UsagePollerTests
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeAccountService _accounts = new(Standard, Privat);
    private readonly FakeUsageProvider _usage = new();

    private UsagePoller Poller() => new(_accounts, _usage, NullLog.Instance);

    private UsagePoller Rechecking(params TimeSpan[] after) => new(_accounts, _usage, NullLog.Instance, after);

    [Fact]
    public async Task A_tick_refreshes_every_account_and_reports_each_result()
    {
        var fresh = new AccountUsage(UsageState.Ok, new UsageSnapshot(new UsageWindow(42, null), null, DateTimeOffset.Now));
        _usage.Fresh["privat"] = fresh;
        using var poller = Poller();
        var updates = new List<(string Id, AccountUsage Usage)>();
        poller.Updated += (account, usage) => updates.Add((account.Id, usage));

        await poller.TickAsync(Ct);

        _usage.Refreshed.ShouldBe(["standard", "privat"], ignoreOrder: true);
        updates.ShouldBe([("standard", _usage.GetCached(Standard)), ("privat", fresh)]);
    }

    [Fact]
    public async Task A_failing_refresh_reports_nothing_and_does_not_throw()
    {
        _usage.ThrowOnRefresh = new HttpRequestException("offline");
        using var poller = Poller();
        var updates = 0;
        poller.Updated += (_, _) => updates++;

        await poller.TickAsync(Ct);

        updates.ShouldBe(0);
    }

    [Fact]
    public void The_poller_asks_every_five_minutes()
    {
        UsagePoller.Interval.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void A_recheck_comes_15_and_60_s_after_the_start()
    {
        UsagePoller.RecheckAfter.ShouldBe([TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60)]);
    }

    [Fact]
    public async Task A_recheck_asks_past_the_guard_while_the_token_is_still_expired()
    {
        var expired = new AccountUsage(UsageState.Expired, null);
        _usage.Fresh["privat"] = expired;
        using var poller = Rechecking(TimeSpan.Zero, TimeSpan.Zero);
        var updates = new List<(string Id, AccountUsage Usage)>();
        poller.Updated += (account, usage) => updates.Add((account.Id, usage));

        await poller.RecheckAsync(Privat);

        _usage.Invalidated.ShouldBe(["privat", "privat"]);
        _usage.Refreshed.ShouldBe(["privat", "privat"]);
        updates.ShouldBe([("privat", expired), ("privat", expired)]);
    }

    [Fact]
    public async Task A_recheck_stops_once_the_limits_are_fine()
    {
        _usage.Fresh["privat"] = new AccountUsage(UsageState.Ok, new UsageSnapshot(null, null, DateTimeOffset.Now));
        using var poller = Rechecking(TimeSpan.Zero, TimeSpan.Zero);

        await poller.RecheckAsync(Privat);

        _usage.Refreshed.ShouldBe(["privat"]);
    }

    [Fact]
    public async Task A_recheck_skips_an_account_removed_meanwhile()
    {
        using var poller = Rechecking(TimeSpan.Zero);

        await poller.RecheckAsync(new Account("weg", "Weg", "#788C5D", IsDefault: false));

        _usage.Refreshed.ShouldBeEmpty();
        _usage.Invalidated.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failing_recheck_reports_nothing_and_tries_again()
    {
        _usage.ThrowOnRefresh = new HttpRequestException("offline");
        using var poller = Rechecking(TimeSpan.Zero, TimeSpan.Zero);
        var updates = 0;
        poller.Updated += (_, _) => updates++;

        await poller.RecheckAsync(Privat);

        updates.ShouldBe(0);
        _usage.Refreshed.ShouldBe(["privat", "privat"]);
    }

    [Fact]
    public async Task A_waiting_recheck_ends_on_dispose()
    {
        var poller = Rechecking(TimeSpan.FromHours(1));
        var recheck = poller.RecheckAsync(Privat);

        poller.Dispose();

        await recheck.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        _usage.Refreshed.ShouldBeEmpty();
    }

    [Fact]
    public void A_started_poller_stops_on_dispose()
    {
        var poller = Poller();

        poller.Start();

        Should.NotThrow(poller.Dispose);
    }

    [Fact]
    public async Task A_throwing_handler_does_not_end_the_polling()
    {
        using var poller = new UsagePoller(_accounts, _usage, NullLog.Instance, interval: TimeSpan.FromMilliseconds(10));
        var calls = 0;
        var again = new TaskCompletionSource();
        poller.Updated += (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("A page failed to show the limits.");
            }

            again.TrySetResult();
        };

        poller.Start();

        await again.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }
}
