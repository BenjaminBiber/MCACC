using Mcacc.App.Localization;
using Mcacc.App.Tray;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Tray;

public sealed class TrayMenuModelTests
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);
    private static readonly Account Arbeit = new("arbeit", "Arbeit", "#D97757", IsDefault: false);
    private static readonly IReadOnlySet<string> NoneRunning = new HashSet<string>();

    private static AccountUsage Usage(double fiveHour, double sevenDay, UsageState state = UsageState.Ok) =>
        new(state, new UsageSnapshot(new UsageWindow(fiveHour, null), new UsageWindow(sevenDay, null), DateTimeOffset.Now));

    private static IReadOnlyList<TrayEntry> Build(
        Func<Account, AccountUsage?>? usageOf = null,
        IReadOnlySet<string>? running = null,
        string? lastId = null) =>
        TrayMenuModel.Build([Standard, Privat, Arbeit], usageOf ?? (_ => null), running ?? NoneRunning, lastId);

    private static IEnumerable<TrayEntry> AccountEntries(IReadOnlyList<TrayEntry> entries) =>
        entries.Where(entry => entry.Children is not null);

    [Fact]
    public void The_menu_opens_the_window_first_and_quits_last()
    {
        var entries = Build();

        entries[0].Action.ShouldBe(TrayAction.Open);
        entries[1].IsSeparator.ShouldBeTrue();
        entries[^1].Action.ShouldBe(TrayAction.Quit);
        entries.Select(entry => entry.Action).ShouldContain(TrayAction.Refresh);
        entries.Select(entry => entry.Action).ShouldContain(TrayAction.Settings);
    }

    [Fact]
    public void Without_a_last_account_the_accounts_keep_their_order()
    {
        AccountEntries(Build()).Select(entry => entry.AccountId).ShouldBe(["standard", "privat", "arbeit"]);
    }

    [Fact]
    public void The_account_used_last_comes_first()
    {
        AccountEntries(Build(lastId: "arbeit")).Select(entry => entry.AccountId).ShouldBe(["arbeit", "standard", "privat"]);
    }

    [Fact]
    public void An_account_line_carries_its_limits_and_whether_it_runs()
    {
        var entries = Build(
            usageOf: account => account == Privat ? Usage(42, 10) : null,
            running: new HashSet<string> { "privat" });

        var privat = AccountEntries(entries).Single(entry => entry.AccountId == "privat");
        privat.Header.ShouldBe(Texts.TrayAccountHeader("Privat", 42, 10, isRunning: true));
        privat.Children!.Where(child => !child.IsEnabled && !child.IsSeparator).Select(child => child.Header).ShouldBe(
        [
            Texts.TrayUsageLine(Texts.UsageFiveHour, 42, null),
            Texts.TrayUsageLine(Texts.UsageSevenDay, 10, null),
        ]);
    }

    [Fact]
    public void Each_account_starts_desktop_and_code_for_itself()
    {
        var arbeit = AccountEntries(Build()).Single(entry => entry.AccountId == "arbeit");

        arbeit.Children!.Where(child => child.Action != TrayAction.None)
            .Select(child => (child.Action, child.AccountId))
            .ShouldBe([(TrayAction.LaunchDesktop, "arbeit"), (TrayAction.LaunchCode, "arbeit")]);
    }

    [Fact]
    public void Without_known_limits_the_submenu_says_so()
    {
        var standard = AccountEntries(Build()).First();

        standard.Header.ShouldBe("Standard");
        standard.Children![0].Header.ShouldBe(Texts.TrayNoUsage);
        standard.Children[0].IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Stale_limits_are_shown_with_the_hint_of_the_tile()
    {
        var usage = Usage(42, 10, UsageState.RateLimited);
        var standard = AccountEntries(Build(usageOf: _ => usage)).First();

        standard.Header.ShouldBe(Texts.TrayAccountHeader("Standard", 42, 10, isRunning: false));
        standard.Children!.Select(child => child.Header).ShouldContain(Texts.UsageAsOf(usage.Snapshot!.FetchedAt));
    }

    [Fact]
    public void A_token_of_another_account_shows_no_limits()
    {
        var usage = Usage(42, 10, UsageState.WrongAccount) with { TokenEmail = "other@example.com" };
        var standard = AccountEntries(Build(usageOf: _ => usage)).First();

        standard.Header.ShouldBe("Standard");
        standard.Children![0].Header.ShouldBe(Texts.UsageWrongAccount("other@example.com"));
    }

    [Fact]
    public void The_tooltip_names_the_account_used_last()
    {
        TrayMenuModel.ToolTip([Standard, Privat], account => account == Privat ? Usage(42, 10) : null, "privat")
            .ShouldBe(Texts.TrayToolTip("Privat", 42, 10));
    }

    [Fact]
    public void Without_accounts_the_tooltip_is_just_the_app()
    {
        TrayMenuModel.ToolTip([], _ => null, lastAccountId: null).ShouldBe("MCACC");
    }
}
