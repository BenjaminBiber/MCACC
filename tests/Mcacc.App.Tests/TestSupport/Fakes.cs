using Mcacc.App.Services;
using Mcacc.App.ViewModels;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Accounts;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Config;
using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Stats;
using Mcacc.Core.Terminal;
using Mcacc.Core.Usage;

namespace Mcacc.App.Tests.TestSupport;

internal sealed class FakeAccountService : IAccountService
{
    public FakeAccountService(params Account[] accounts)
    {
        var list = accounts.Length > 0 ? accounts : [Account.CreateDefault()];
        Config = new AppConfig(AppConfig.CurrentVersion, list, new AppSettings());
        LoadResult = new ConfigLoadResult(Config, ConfigLoadStatus.Loaded, IsReadOnly: false, Repairs: []);
    }

    public ConfigLoadResult LoadResult { get; set; }

    public AppConfig Config { get; private set; }

    public List<(string Name, string Color, CopyRequest? Copy)> Created { get; } = [];

    public List<(string Id, string Name, string Color)> Updated { get; } = [];

    public List<(string Id, bool DeleteData)> Removed { get; } = [];

    public Exception? ThrowOnCreate { get; set; }

    public Exception? ThrowOnRemove { get; set; }

    public CopyReport? CreateReport { get; set; }

    public CreateAccountResult Create(string name, string color, CopyRequest? copy)
    {
        Created.Add((name, color, copy));
        if (ThrowOnCreate is { } error)
        {
            throw error;
        }

        var account = new Account(SlugGenerator.Generate(name, _ => false), name.Trim(), color, IsDefault: false);
        Config = Config with { Accounts = [.. Config.Accounts, account] };
        return new CreateAccountResult(account, CreateReport);
    }

    public Account Update(string id, string name, string color)
    {
        Updated.Add((id, name, color));
        var accounts = Config.Accounts.ToList();
        var index = accounts.FindIndex(account => account.Id == id);
        accounts[index] = accounts[index] with { Name = name.Trim(), Color = color };
        Config = Config with { Accounts = accounts };
        return accounts[index];
    }

    public void Remove(string id, bool deleteData)
    {
        Removed.Add((id, deleteData));
        if (ThrowOnRemove is { } error)
        {
            throw error;
        }

        Config = Config with { Accounts = [.. Config.Accounts.Where(account => account.Id != id)] };
    }

    public void UpdateSettings(Func<AppSettings, AppSettings> change) =>
        Config = Config with { Settings = change(Config.Settings) };
}

internal sealed class FakeDesktopLauncher : IDesktopLauncher
{
    public DesktopLaunchCheck Check { get; set; } = new DesktopLaunchCheck.Ready();

    public DesktopLaunchOutcome Outcome { get; set; } = DesktopLaunchOutcome.Launched;

    public List<bool> Launches { get; } = [];

    public int Prepares { get; private set; }

    public Task<DesktopLaunchCheck> PrepareAsync(Account target, IReadOnlyList<Account> accounts, CancellationToken ct)
    {
        Prepares++;
        return Task.FromResult(Check);
    }

    public Task<DesktopLaunchOutcome> LaunchAsync(
        Account target,
        IReadOnlyList<Account> accounts,
        bool quitConflicts,
        CancellationToken ct)
    {
        Launches.Add(quitConflicts);
        return Task.FromResult(Outcome);
    }
}

internal sealed class FakeLoginRedirector : IDesktopLoginRedirector
{
    public DesktopRedirectOutcome Outcome { get; set; } = DesktopRedirectOutcome.NotSeen;

    public List<Account> Watched { get; } = [];

    /// <summary>Set it to hold the watch open, the way the real one holds the window open.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public async Task<DesktopRedirectOutcome> WatchAsync(Account target, CancellationToken ct)
    {
        Watched.Add(target);
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(ct).ConfigureAwait(false);
        }

        return Outcome;
    }
}

internal sealed class FakeCodeLauncher : ICodeLauncher
{
    public CodeLaunchResult Result { get; set; } =
        new(CodeLaunchOutcome.Launched, new TerminalOption(TerminalIds.Cmd, "Eingabeaufforderung", "cmd.exe"), false);

    public List<Account> Launched { get; } = [];

    public CodeLaunchResult Launch(Account account, AppSettings settings)
    {
        Launched.Add(account);
        return Result;
    }
}

internal sealed class FakeUsageProvider : IUsageProvider
{
    public Dictionary<string, AccountUsage> Cached { get; } = [];

    public Dictionary<string, AccountUsage> Fresh { get; } = [];

    public List<string> Refreshed { get; } = [];

    public Exception? ThrowOnRefresh { get; set; }

    public Dictionary<string, AccountProfile> Profiles { get; } = [];

    public Dictionary<string, List<UsageHistoryPoint>> History { get; } = [];

    public List<string> Forgotten { get; } = [];

    public AccountUsage GetCached(Account account) =>
        Cached.GetValueOrDefault(account.Id, new AccountUsage(UsageState.NotLoggedIn, null));

    public Task<AccountUsage> RefreshAsync(Account account, CancellationToken ct)
    {
        Refreshed.Add(account.Id);
        if (ThrowOnRefresh is { } error)
        {
            throw error;
        }

        return Task.FromResult(Fresh.GetValueOrDefault(account.Id, GetCached(account)));
    }

    public AccountProfile? GetProfile(Account account) => Profiles.GetValueOrDefault(account.Id);

    public IReadOnlyList<UsageHistoryPoint> GetHistory(Account account, DateTimeOffset since) =>
        History.TryGetValue(account.Id, out var points) ? points.Where(point => point.At >= since).ToList() : [];

    public Exception? ThrowOnForget { get; set; }

    public void Forget(Account account)
    {
        Forgotten.Add(account.Id);
        if (ThrowOnForget is { } error)
        {
            throw error;
        }
    }
}

internal sealed class FakeLoginReader : ILoginInfoReader
{
    public Dictionary<string, ClaudeLoginInfo> ByClaudeDir { get; } = [];

    public ClaudeLoginInfo? Read(AccountPaths paths) => ByClaudeDir.GetValueOrDefault(paths.EffectiveClaudeDir);
}

internal sealed class FakeDetector : IDesktopInstanceDetector
{
    public List<DesktopInstance> Instances { get; } = [];

    public Exception? ThrowOnDetect { get; set; }

    public int Calls { get; private set; }

    public Task<IReadOnlyList<DesktopInstance>> DetectAsync(CancellationToken ct)
    {
        Calls++;
        if (ThrowOnDetect is { } error)
        {
            throw error;
        }

        return Task.FromResult<IReadOnlyList<DesktopInstance>>(Instances);
    }
}

internal sealed class FakeTerminalCatalog : ITerminalCatalog
{
    public IReadOnlyList<TerminalOption> Installed { get; set; } =
        [new TerminalOption(TerminalIds.WindowsTerminal, "Windows Terminal", "wt.exe")];

    public string PlatformDefaultId { get; set; } = TerminalIds.WindowsTerminal;

    public IReadOnlyList<TerminalOption> GetInstalled() => Installed;
}

internal sealed class FakeShellIntegration : IShellIntegration
{
    public List<string> Revealed { get; } = [];

    public List<Uri> Opened { get; } = [];

    public Exception? ThrowOnReveal { get; set; }

    public void RevealFolder(string path)
    {
        Revealed.Add(path);
        if (ThrowOnReveal is { } error)
        {
            throw error;
        }
    }

    public void OpenUrl(Uri url) => Opened.Add(url);
}

internal sealed class FakeAppLifetime : IAppLifetime
{
    public int Shows { get; private set; }

    public int Hides { get; private set; }

    public int Shutdowns { get; private set; }

    public void ShowWindow() => Shows++;

    public void HideWindow() => Hides++;

    public void Shutdown() => Shutdowns++;
}

/// <summary>Answers each dialog from a scripted queue and records what was shown.</summary>
internal sealed class FakeDialogService : IDialogService
{
    private readonly Queue<Func<DialogViewModel, object?>> _answers = new();

    public List<DialogViewModel> Shown { get; } = [];

    public List<string> BusyTexts { get; } = [];

    public void Answer<TDialog>(Func<TDialog, object?> answer)
        where TDialog : DialogViewModel =>
        _answers.Enqueue(dialog => dialog is TDialog typed ? answer(typed) : null);

    public Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        Shown.Add(dialog);
        var answer = _answers.TryDequeue(out var next) ? next(dialog) : null;
        return Task.FromResult(answer is TResult typed ? typed : default);
    }

    public IDisposable Busy(string text)
    {
        BusyTexts.Add(text);
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

internal sealed class FakeClaudeStatsProvider : IClaudeStatsProvider
{
    public List<TokenBucket> Buckets { get; } = [];

    public List<string> Loaded { get; } = [];

    /// <summary>The token of the last load, to see whether leaving the page reached the scan.</summary>
    public CancellationToken LoadToken { get; private set; }

    public int Completed { get; private set; }

    public List<string> Forgotten { get; } = [];

    /// <summary>Holds a load until it is completed, to look at the page while it scans.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public Exception? ThrowOnLoad { get; set; }

    public async Task<IReadOnlyList<TokenBucket>> LoadAsync(
        Account account,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        Loaded.Add(account.Id);
        LoadToken = ct;
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(ct).ConfigureAwait(true);
        }

        if (ThrowOnLoad is { } error)
        {
            throw error;
        }

        Completed++;
        return Buckets.ToList();
    }

    public List<ClaudeDailyTokens> Summary { get; } = [];

    public IReadOnlyList<ClaudeDailyTokens> LoadSummary(Account account) => Summary.ToList();

    public void Forget(Account account) => Forgotten.Add(account.Id);
}

internal sealed class FakeCredentialSource : ICredentialSource
{
    public Dictionary<string, CredentialReadResult> Results { get; } = [];

    public Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct) =>
        Task.FromResult(Results.GetValueOrDefault(
            account.Id,
            new CredentialReadResult(CredentialReadStatus.NotLoggedIn, null)));
}
