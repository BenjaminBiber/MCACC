using System.Globalization;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Controls;
using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Stats;
using Mcacc.Core.Usage;

namespace Mcacc.App.ViewModels;

/// <summary>Everything MCACC can read about one account: profile, limits with their history, and Claude Code usage.</summary>
public sealed partial class AccountInfoViewModel : PageViewModel
{
    /// <summary>In hours: two history points further apart are not connected, the app was closed in between.</summary>
    internal const double HistoryDayGap = 0.5;

    internal const double HistoryWeekGap = 2;

    private const int NamedModels = 5;

    private static readonly ChartColor[] ModelColors =
        [ChartColor.Series1, ChartColor.Series2, ChartColor.Series3, ChartColor.Series4, ChartColor.Series5];

    private readonly AppServices _services;
    private CancellationTokenSource? _life;
    private IReadOnlyList<TokenBucket> _buckets = [];
    private AccountProfile? _profile;
    private ClaudeLoginInfo? _login;
    private CredentialFacts? _credentials;
    private string? _desktop;

    public AccountInfoViewModel(AppServices services, Account account)
    {
        _services = services;
        Account = account;
        _profile = services.Usage.GetProfile(account);
        _login = services.Login.Read(services.Resolver.Resolve(account));
        ShowLimits(services.Usage.GetCached(account));
        LoadHistory();
        BuildFields();
        ShowStats();
    }

    public Account Account { get; }

    public string Title => Texts.Info;

    public string Subtitle => Texts.InfoSubtitle;

    public string AccountName => Account.Name;

    public string BackTooltip => Texts.Back;

    public string RefreshTooltip => Texts.RefreshUsage;

    public string Initials => AccountTileViewModel.InitialsOf(Account.Name);

    public IBrush AvatarBrush => new SolidColorBrush(Color.Parse(Account.Color));

    public string AccountHeader => Texts.InfoAccount;

    public string LimitsHeader => Texts.InfoLimits;

    public string HistoryHeader => Texts.InfoHistory;

    public string UsageHeader => Texts.InfoUsage;

    public string TokensPerDayHeader => Texts.InfoTokensPerDay;

    public string KindsHeader => Texts.InfoTokenKinds;

    public string ProjectsHeader => Texts.InfoTopProjects;

    public string ModelsHeader => Texts.InfoModels;

    public string ColumnModel => Texts.InfoColumnModel;

    public string ColumnTokens => Texts.InfoColumnTokens;

    public string ColumnShare => Texts.InfoColumnShare;

    public string ColumnCost => Texts.InfoColumnCost;

    public string CostNote => Texts.InfoCostNote;

    public string NoAccountDataText => Texts.InfoNoAccountData;

    public string NoLimitsText => Texts.InfoNoLimits;

    public string NoHistoryText => Texts.InfoNoHistory;

    public string NoSessionsText => Texts.InfoNoSessions;

    public string HistoryDayText => Texts.InfoHistoryDay;

    public string HistoryWeekText => Texts.InfoHistoryWeek;

    public string HistoryEndLabel => Texts.InfoNow;

    public string Days7Text => Texts.InfoDays(7);

    public string Days30Text => Texts.InfoDays(30);

    public string Days90Text => Texts.InfoDays(90);

    public string KindsCaption => Texts.InfoTokens;

    public IReadOnlyList<LegendItemViewModel> HistoryLegend { get; } =
    [
        new(Texts.UsageFiveHour, ChartColor.Series1),
        new(Texts.UsageSevenDay, ChartColor.Series2),
    ];

    public Func<double, string> TokenAxis { get; } = value => Texts.TokenAmount((long)Math.Round(value));

    // Account

    [ObservableProperty]
    public partial IReadOnlyList<InfoFieldViewModel> Fields { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasAccountData { get; private set; }

    // Limits

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRings))]
    public partial IReadOnlyList<LimitRingViewModel> Rings { get; private set; } = [];

    public bool HasRings => Rings.Count > 0;

    [ObservableProperty]
    public partial string? LimitsAsOf { get; private set; }

    [ObservableProperty]
    public partial string? LimitsHint { get; private set; }

    // History

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHistoryDay))]
    public partial bool IsHistoryWeek { get; private set; }

    public bool IsHistoryDay => !IsHistoryWeek;

    [ObservableProperty]
    public partial IReadOnlyList<LineSeries> HistorySeries { get; private set; } = [];

    [ObservableProperty]
    public partial double HistoryMax { get; private set; } = 24;

    [ObservableProperty]
    public partial double HistoryGap { get; private set; } = HistoryDayGap;

    [ObservableProperty]
    public partial string HistoryStartLabel { get; private set; } = Texts.InfoHistoryDayStart;

    // Claude Code usage

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDays7), nameof(IsDays30), nameof(IsDays90))]
    public partial int UsageDays { get; private set; } = 30;

    public bool IsDays7 => UsageDays == 7;

    public bool IsDays30 => UsageDays == 30;

    public bool IsDays90 => UsageDays == 90;

    [ObservableProperty]
    public partial bool IsScanning { get; private set; }

    [ObservableProperty]
    public partial string ScanText { get; private set; } = Texts.InfoScanning;

    [ObservableProperty]
    public partial string? StatsError { get; private set; }

    [ObservableProperty]
    public partial bool HasSessions { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<KpiViewModel> Kpis { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<BarStack> DailyBars { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LegendItemViewModel> ModelLegend { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ChartSlice> KindSlices { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LegendItemViewModel> KindLegend { get; private set; } = [];

    [ObservableProperty]
    public partial string? KindCenterText { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<BarListItem> Projects { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ModelRowViewModel> Models { get; private set; } = [];

    [ObservableProperty]
    public partial string? UnpricedText { get; private set; }

    public override async Task ActivateAsync(CancellationToken ct)
    {
        _services.Poller.Updated -= OnPolled;
        _services.Poller.Updated += OnPolled;
        _life?.Cancel();
        _life?.Dispose();
        _life = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _life.Token);
        await Task.WhenAll(
                RefreshLimitsAsync(linked.Token),
                LoadStatsAsync(_life.Token),
                LoadAccountAsync(linked.Token))
            .ConfigureAwait(true);
    }

    public override void Deactivate()
    {
        _services.Poller.Updated -= OnPolled;
        _life?.Cancel();
    }

    [RelayCommand]
    public void Back() => _services.Navigator.Back();

    [RelayCommand]
    public Task RefreshAsync() => RefreshLimitsAsync(_life?.Token ?? CancellationToken.None);

    /// <param name="range">"24h" or "7d".</param>
    [RelayCommand]
    public void ShowHistory(string range)
    {
        IsHistoryWeek = range == "7d";
        LoadHistory();
    }

    /// <param name="days">"7", "30" or "90".</param>
    [RelayCommand]
    public void ShowDays(string days)
    {
        UsageDays = int.Parse(days, CultureInfo.InvariantCulture);
        ShowStats();
    }

    public override bool OnKeyDown(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case Key.F5 when modifiers == KeyModifiers.None:
                RefreshCommand.Execute(null);
                return true;
            case Key.Escape or Key.Back:
            case Key.Left when modifiers.HasFlag(KeyModifiers.Alt):
                Back();
                return true;
            default:
                return false;
        }
    }

    private void OnPolled(Account account, AccountUsage usage)
    {
        if (account.Id == Account.Id)
        {
            ShowUsage(usage);
        }
    }

    private async Task RefreshLimitsAsync(CancellationToken ct)
    {
        try
        {
            ShowUsage(await _services.Usage.RefreshAsync(Account, ct).ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            // The page was left.
        }
        catch (Exception e)
        {
            _services.Log.Warn($"Reading the limits of '{Account.Id}' failed.", e);
        }
    }

    /// <summary>A fresh reply may bring a new profile and a new history point too.</summary>
    private void ShowUsage(AccountUsage usage)
    {
        ShowLimits(usage);
        LoadHistory();
        _profile = _services.Usage.GetProfile(Account);
        BuildFields();
    }

    private void ShowLimits(AccountUsage usage)
    {
        var rings = new List<LimitRingViewModel>();
        if (usage.Snapshot is { } snapshot)
        {
            AddRing(rings, Texts.UsageFiveHour, snapshot.FiveHour);
            AddRing(rings, Texts.UsageSevenDay, snapshot.SevenDay);
            foreach (var (key, window) in (snapshot.Extra ?? new Dictionary<string, UsageWindow>())
                         .OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                AddRing(rings, Texts.UsageExtraWindow(key), window);
            }
        }

        Rings = rings;
        LimitsAsOf = usage.Snapshot is { } shown ? Texts.UsageAsOf(shown.FetchedAt) : null;
        LimitsHint = AccountTileViewModel.HintFor(usage);
    }

    private static void AddRing(List<LimitRingViewModel> rings, string label, UsageWindow? window)
    {
        if (window?.Utilization is not null)
        {
            rings.Add(LimitRingViewModel.From(label, window));
        }
    }

    private void LoadHistory()
    {
        var span = IsHistoryWeek ? TimeSpan.FromDays(7) : TimeSpan.FromHours(24);
        var start = DateTimeOffset.Now - span;
        var points = _services.Usage.GetHistory(Account, start);
        HistoryMax = span.TotalHours;
        HistoryGap = IsHistoryWeek ? HistoryWeekGap : HistoryDayGap;
        HistoryStartLabel = IsHistoryWeek ? Texts.InfoHistoryWeekStart : Texts.InfoHistoryDayStart;
        HistorySeries = points.Count == 0
            ? []
            :
            [
                Line(Texts.UsageFiveHour, ChartColor.Series1, points, start, point => point.FiveHour),
                Line(Texts.UsageSevenDay, ChartColor.Series2, points, start, point => point.SevenDay),
            ];
    }

    private static LineSeries Line(
        string name,
        ChartColor color,
        IReadOnlyList<UsageHistoryPoint> points,
        DateTimeOffset start,
        Func<UsageHistoryPoint, double?> value) =>
        new(name, color, points
            .Where(point => value(point) is not null)
            .Select(point => new ChartPoint(
                (point.At - start).TotalHours,
                value(point)!.Value,
                Texts.HistoryTooltip(point.At, point.FiveHour, point.SevenDay)))
            .ToList());

    /// <summary>
    /// The scan itself is not canceled with the page: a first scan of a large log folder would otherwise start over
    /// each time and its index would never be saved. <paramref name="life"/> only stops a left page from being
    /// updated; opening the page again waits for the running scan and then finds the index up to date.
    /// </summary>
    private async Task LoadStatsAsync(CancellationToken life)
    {
        IsScanning = true;
        StatsError = null;
        ScanText = Texts.InfoScanning;
        var progress = new Progress<double>(value =>
        {
            if (!life.IsCancellationRequested)
            {
                ScanText = Texts.InfoScanProgress(value);
            }
        });
        try
        {
            var buckets = await _services.Stats.LoadAsync(Account, progress, CancellationToken.None).ConfigureAwait(true);
            if (!life.IsCancellationRequested)
            {
                _buckets = buckets;
                ShowStats();
            }
        }
        catch (Exception e)
        {
            _services.Log.Warn($"Reading the Claude Code logs of '{Account.Id}' failed.", e);
            if (!life.IsCancellationRequested)
            {
                StatsError = Texts.InfoScanFailed;
            }
        }
        finally
        {
            // A page opened again runs its own scan; this one must not clear that one's flag.
            if (!life.IsCancellationRequested)
            {
                IsScanning = false;
            }
        }
    }

    private void ShowStats()
    {
        var stats = AccountStatsQuery.Compute(_buckets, DateTimeOffset.Now, UsageDays, TimeZoneInfo.Local);
        var total = stats.Tokens.Total;
        HasSessions = total > 0;

        // Without a priced model the cost has no source and is left out like any missing value; the model table and
        // the unpriced note below still say why.
        KpiViewModel[] cost = stats.Cost is { } usd ? [new(Texts.InfoCost, Texts.Cost(usd), Texts.InfoCostNote)] : [];
        Kpis =
        [
            new(Texts.InfoTokens, Texts.TokenAmount(total)),
            .. cost,
            new(Texts.InfoSessions, Texts.Count(stats.Sessions)),
            new(Texts.InfoMessages, Texts.Count(stats.Messages)),
            new(Texts.InfoActiveDays, Texts.InfoActiveDaysOf(stats.ActiveDays, UsageDays)),
        ];

        var colors = stats.Models
            .Take(NamedModels)
            .Select((model, index) => (model.Model, Color: ModelColors[index]))
            .ToDictionary(entry => entry.Model, entry => entry.Color, StringComparer.Ordinal);
        ChartColor ColorOf(string model) => colors.GetValueOrDefault(model, ChartColor.Series6);

        DailyBars = stats.Days
            .Select(day => new BarStack(
                Texts.DayLabel(day.Day),
                day.TokensByModel
                    .GroupBy(entry => ColorOf(entry.Key))
                    .OrderBy(group => group.Key)
                    .Select(group => new BarSegment(group.Sum(entry => entry.Value), group.Key))
                    .ToList(),
                day.Total > 0 ? Texts.DayTooltip(day) : null))
            .ToList();

        var legend = stats.Models
            .Take(NamedModels)
            .Select(model => new LegendItemViewModel(Texts.ModelName(model.Model), ColorOf(model.Model)))
            .ToList();
        if (stats.Models.Count > NamedModels)
        {
            legend.Add(new LegendItemViewModel(Texts.InfoOtherModels, ChartColor.Series6));
        }

        ModelLegend = legend;

        var tokens = stats.Tokens;
        KindSlices =
        [
            Kind(Texts.InfoKindInput, tokens.Input, ChartColor.Series1, total),
            Kind(Texts.InfoKindOutput, tokens.Output, ChartColor.Series2, total),
            Kind(Texts.InfoKindCacheWrite, tokens.CacheWrite, ChartColor.Series3, total),
            Kind(Texts.InfoKindCacheRead, tokens.CacheRead, ChartColor.Series4, total),
        ];
        KindLegend = KindSlices
            .Select(slice => new LegendItemViewModel(slice.Label, slice.Color, Texts.TokenAmount((long)slice.Value)))
            .ToList();
        KindCenterText = Texts.TokenAmount(total);

        Projects = stats.Projects
            .Select(project => new BarListItem(
                Texts.ProjectName(project.Project),
                project.Tokens,
                Texts.TokenAmount(project.Tokens),
                ChartColor.Accent,
                Texts.ProjectTooltip(project.Project, project.Tokens, project.Cost)))
            .ToList();

        Models = stats.Models
            .Select(model => new ModelRowViewModel(
                Texts.ModelName(model.Model),
                Texts.TokenAmount(model.Tokens.Total),
                Texts.Share(total > 0 ? (double)model.Tokens.Total / total : 0),
                model.Cost is { } cost ? Texts.Cost(cost) : Texts.InfoNoValue,
                ColorOf(model.Model)))
            .ToList();

        UnpricedText = stats.UnpricedTokens > 0 ? Texts.InfoUnpriced(stats.UnpricedTokens) : null;
    }

    private static ChartSlice Kind(string label, long value, ChartColor color, long total) =>
        new(label, value, color, Texts.AmountTooltip(label, value, total));

    /// <summary>Reads the credentials and the running desktop; both need IO, so the constructor leaves them out.</summary>
    private async Task LoadAccountAsync(CancellationToken ct)
    {
        try
        {
            var result = await _services.Credentials
                .ReadAsync(Account, _services.Resolver.Resolve(Account), ct)
                .ConfigureAwait(true);

            // Only what the page shows; the access token stays in this method.
            _credentials = result.Credentials is { } credentials
                ? new CredentialFacts(credentials.SubscriptionType, credentials.Scopes, credentials.ExpiresAt)
                : null;

            var instances = await _services.Detector.DetectAsync(ct).ConfigureAwait(true);
            var own = instances
                .Where(instance => _services.Matcher.Match(instance, _services.Accounts.Config.Accounts)?.Id == Account.Id)
                .ToList();
            var since = own.Select(instance => instance.StartTime).Min();
            _desktop = own.Count == 0
                ? Texts.InfoDesktopNotRunning
                : since is { } start ? Texts.InfoSince(start, DateTimeOffset.Now) : Texts.RunningBadge;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            _services.Log.Warn($"Reading the account details of '{Account.Id}' failed.", e);
        }

        BuildFields();
    }

    private void BuildFields()
    {
        var profile = _profile;
        var login = _login;
        var credentials = _credentials;
        var fields = new List<InfoFieldViewModel>();
        AddField(fields, Texts.InfoEmail, profile?.Email ?? login?.Email);
        AddField(fields, Texts.InfoName, profile?.FullName ?? profile?.DisplayName ?? login?.DisplayName);
        AddField(fields, Texts.InfoPlan, Texts.Plan(profile?.HasClaudeMax, profile?.HasClaudePro, credentials?.SubscriptionType));
        AddField(fields, Texts.InfoOrganization, profile?.OrganizationName ?? login?.OrganizationName);
        AddField(fields, Texts.InfoRole, Humanized(login?.OrganizationRole));
        AddField(fields, Texts.InfoBilling, Humanized(profile?.BillingType));
        AddField(fields, Texts.InfoRateLimitTier, Humanized(profile?.RateLimitTier));
        AddField(fields, Texts.InfoCreatedAt, profile?.CreatedAt is { } created ? Texts.InfoDate(created) : null);
        AddField(fields, Texts.InfoTokenExpires, credentials?.ExpiresAt is { } expires ? Texts.InfoDateTime(expires) : null);
        AddField(fields, Texts.InfoScopes, credentials?.Scopes is { Count: > 0 } scopes ? string.Join(", ", scopes) : null);
        AddField(fields, Texts.InfoStartups, login?.NumStartups is { } startups ? Texts.Count(startups) : null);
        AddField(fields, Texts.InfoInstallMethod, Humanized(login?.InstallMethod));
        AddField(fields, Texts.InfoProjects, login?.ProjectCount is { } projects ? Texts.Count(projects) : null);
        HasAccountData = fields.Count > 0;
        AddField(fields, Texts.InfoDesktop, _desktop);
        Fields = fields;
    }

    private static string? Humanized(string? value) => string.IsNullOrWhiteSpace(value) ? null : Texts.Humanize(value);

    private static void AddField(List<InfoFieldViewModel> fields, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new InfoFieldViewModel(label, value));
        }
    }

    /// <summary>What the page shows of the credentials: never the token.</summary>
    private sealed record CredentialFacts(string? SubscriptionType, IReadOnlyList<string>? Scopes, DateTimeOffset? ExpiresAt);
}
