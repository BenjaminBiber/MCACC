using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mcacc.App.Localization;
using Mcacc.Core.Model;
using Mcacc.Core.Providers;
using Mcacc.Core.Usage;

namespace Mcacc.App.ViewModels;

/// <summary>One account tile: who it is, whether Claude runs with it, and what is left of its limits.</summary>
public sealed partial class AccountTileViewModel : ViewModelBase
{
    public AccountTileViewModel(Account account)
    {
        Account = account;
        FiveHour = new UsageWindowViewModel(Texts.UsageFiveHour);
        SevenDay = new UsageWindowViewModel(Texts.UsageSevenDay);
    }

    public Account Account { get; private set; }

    public string Id => Account.Id;

    public string Name => Account.Name;

    public bool IsDefault => Account.IsDefault;

    public string Initials => InitialsOf(Account.Name);

    public IBrush AvatarBrush => new SolidColorBrush(Color.Parse(Account.Color));

    public UsageWindowViewModel FiveHour { get; }

    public UsageWindowViewModel SevenDay { get; }

    /// <summary>The login e-mail, or a hint that the account has none yet.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; private set; } = Texts.NotLoggedIn;

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string? UsageHint { get; private set; }

    public string? Badge => IsRunning ? Texts.RunningBadge : null;

    /// <summary>
    /// Which service the account belongs to. Null for Claude: MCACC was a Claude switcher, so an unmarked tile
    /// reads as Claude, and only the accounts that need telling apart carry a word.
    /// </summary>
    public string? ProviderBadge =>
        Account.Provider == ProviderKind.Claude ? null : Texts.ProviderName(Account.Provider);

    public static string InitialsOf(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var letters = words.Take(2).Select(word => char.ToUpperInvariant(word[0]));
        var initials = string.Concat(letters);
        return initials.Length > 0 ? initials : "?";
    }

    public void Update(Account account)
    {
        Account = account;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(AvatarBrush));
        OnPropertyChanged(nameof(IsDefault));
        OnPropertyChanged(nameof(ProviderBadge));
    }

    /// <param name="login">Null when the account has no login; its e-mail is null for an API-key login.</param>
    public void SetLogin(AccountLoginInfo? login) => Subtitle = login switch
    {
        null => Texts.NotLoggedIn,
        { Email: { } email } when !string.IsNullOrWhiteSpace(email) => email,
        _ => Texts.LoggedInWithApiKey,
    };

    public void SetUsage(AccountUsage usage)
    {
        FiveHour.Set(usage.Snapshot?.FiveHour);
        SevenDay.Set(usage.Snapshot?.SevenDay);
        UsageHint = HintFor(Account.Provider, usage);
    }

    internal static string? HintFor(ProviderKind provider, AccountUsage usage) => usage.State switch
    {
        UsageState.Expired => Texts.UsageExpired(provider),
        UsageState.WrongAccount => Texts.UsageWrongAccount(provider, usage.TokenEmail),
        UsageState.NotLoggedIn => null,
        UsageState.Ok => null,
        _ => usage.Snapshot is { } snapshot ? Texts.UsageAsOf(snapshot.FetchedAt) : null,
    };

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(Badge));
}

/// <summary>One usage bar's data; the control turns it into the bar and the line above it.</summary>
public sealed partial class UsageWindowViewModel(string label) : ViewModelBase
{
    public string Label { get; } = label;

    [ObservableProperty]
    public partial double? Value { get; private set; }

    [ObservableProperty]
    public partial string? ResetText { get; private set; }

    public void Set(UsageWindow? window)
    {
        Value = window?.Utilization;
        ResetText = window?.ResetsAt is { } resetsAt ? Texts.UsageReset(resetsAt) : null;
    }
}
