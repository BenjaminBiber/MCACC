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
    public void A_started_poller_stops_on_dispose()
    {
        var poller = Poller();

        poller.Start();

        Should.NotThrow(poller.Dispose);
    }
}
