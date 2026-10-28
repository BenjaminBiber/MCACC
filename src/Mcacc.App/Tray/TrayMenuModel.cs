using Mcacc.App.Localization;
using Mcacc.App.ViewModels;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.App.Tray;

public enum TrayAction
{
    None,
    Open,
    LaunchDesktop,
    LaunchCode,
    Refresh,
    Settings,
    Quit,
}

/// <summary>One line of the tray menu, free of Avalonia so the menu can be tested as data.</summary>
/// <param name="AccountId">The account a launch is for.</param>
/// <param name="Children">Makes the line a submenu.</param>
public sealed record TrayEntry(
    string Header,
    TrayAction Action = TrayAction.None,
    string? AccountId = null,
    IReadOnlyList<TrayEntry>? Children = null,
    bool IsEnabled = true,
    bool IsSeparator = false)
{
    public static readonly TrayEntry Separator = new("-", IsEnabled: false, IsSeparator: true);
}

/// <summary>What the tray menu shows, built anew from the accounts and their last known limits.</summary>
public static class TrayMenuModel
{
    public static IReadOnlyList<TrayEntry> Build(
        IReadOnlyList<Account> accounts,
        Func<Account, AccountUsage?> usageOf,
        IReadOnlySet<string> running,
        string? lastAccountId)
    {
        var entries = new List<TrayEntry> { new(Texts.TrayOpen, TrayAction.Open), TrayEntry.Separator };
        entries.AddRange(Ordered(accounts, lastAccountId)
            .Select(account => AccountEntry(account, usageOf(account), running.Contains(account.Id))));
        entries.Add(TrayEntry.Separator);
        entries.Add(new TrayEntry(Texts.RefreshUsage, TrayAction.Refresh));
        entries.Add(new TrayEntry(Texts.TraySettings, TrayAction.Settings));
        entries.Add(TrayEntry.Separator);
        entries.Add(new TrayEntry(Texts.TrayQuit, TrayAction.Quit));
        return entries;
    }

    /// <summary>The tooltip names the account used last, or the first one when none was used yet.</summary>
    public static string ToolTip(IReadOnlyList<Account> accounts, Func<Account, AccountUsage?> usageOf, string? lastAccountId)
    {
        if (Ordered(accounts, lastAccountId).FirstOrDefault() is not { } account)
        {
            return Texts.TrayToolTip(accountName: null, fiveHour: null, sevenDay: null);
        }

        var snapshot = SnapshotOf(usageOf(account));
        return Texts.TrayToolTip(account.Name, snapshot?.FiveHour?.Utilization, snapshot?.SevenDay?.Utilization);
    }

    /// <summary>The account used last on top, where the pointer lands first; the others as the picker orders them.</summary>
    private static IEnumerable<Account> Ordered(IReadOnlyList<Account> accounts, string? lastAccountId) =>
        accounts.Where(account => account.Id == lastAccountId)
            .Concat(accounts.Where(account => account.Id != lastAccountId));

    private static TrayEntry AccountEntry(Account account, AccountUsage? usage, bool isRunning)
    {
        var snapshot = SnapshotOf(usage);
        var children = new List<TrayEntry>();
        AddUsageLine(children, Texts.UsageFiveHour, snapshot?.FiveHour);
        AddUsageLine(children, Texts.UsageSevenDay, snapshot?.SevenDay);
        if ((usage is null ? null : AccountTileViewModel.HintFor(account.Provider, usage)) is { } hint)
        {
            children.Add(new TrayEntry(hint, IsEnabled: false));
        }
        else if (children.Count == 0)
        {
            children.Add(new TrayEntry(Texts.TrayNoUsage, IsEnabled: false));
        }

        children.Add(TrayEntry.Separator);

        // A provider whose desktop app MCACC cannot start per profile gets no entry for it.
        if (ProviderDescriptors.For(account).Desktop is not null)
        {
            children.Add(new TrayEntry(Texts.TrayLaunchDesktop(account.Provider), TrayAction.LaunchDesktop, account.Id));
        }

        children.Add(new TrayEntry(Texts.TrayLaunchCode(account.Provider), TrayAction.LaunchCode, account.Id));

        var header = Texts.TrayAccountHeader(
            account.Name,
            snapshot?.FiveHour?.Utilization,
            snapshot?.SevenDay?.Utilization,
            isRunning);
        return new TrayEntry(header, AccountId: account.Id, Children: children);
    }

    private static void AddUsageLine(List<TrayEntry> children, string label, UsageWindow? window)
    {
        if (window?.Utilization is { } utilization)
        {
            children.Add(new TrayEntry(Texts.TrayUsageLine(label, utilization, window.ResetsAt), IsEnabled: false));
        }
    }

    /// <summary>A token of another account says nothing about this one, so its limits are not shown at all.</summary>
    private static UsageSnapshot? SnapshotOf(AccountUsage? usage) =>
        usage is null || usage.State == UsageState.WrongAccount ? null : usage.Snapshot;
}
