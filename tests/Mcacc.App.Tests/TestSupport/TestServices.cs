using Mcacc.App.Services;
using Mcacc.App.ViewModels;
using Mcacc.Core.Desktop;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.App.Tests.TestSupport;

/// <summary>An <see cref="AppServices"/> made of fakes, with a temp data root so nothing touches the real one.</summary>
internal sealed class TestServices : IDisposable
{
    private readonly TempDirectory _temp = new();

    public TestServices(OsKind os = OsKind.Windows, params Account[] accounts)
        : this(null, os, accounts)
    {
    }

    /// <param name="recheckAfter">The waits of <see cref="UsagePoller.RecheckAsync"/>; null keeps the real ones.</param>
    public TestServices(IReadOnlyList<TimeSpan>? recheckAfter, OsKind os, params Account[] accounts)
    {
        Platform = new PlatformInfo(
            os,
            Path.Join(_temp.Path, "home"),
            Path.Join(_temp.Path, "home", "AppData", "Roaming"),
            Path.Join(_temp.Path, "home", "AppData", "Local"));
        Paths = new McaccPaths(Path.Join(_temp.Path, ".mcacc"), Platform.PathStyle);
        Accounts = new FakeAccountService(accounts);
        var resolver = new AccountPathResolver(Paths, Platform);
        Navigator = new Navigator();
        Window = new MainWindowViewModel(Navigator);
        Poller = new UsagePoller(Accounts, Usage, NullLog.Instance, recheckAfter);
        Services = new AppServices(
            Accounts,
            Desktop,
            Redirect,
            Code,
            Usage,
            Poller,
            Stats,
            Credentials,
            Login,
            Detector,
            new DesktopInstanceMatcher(resolver, Platform.PathStyle),
            Terminals,
            resolver,
            Paths,
            Platform,
            Integration,
            Lifetime,
            NullLog.Instance,
            Navigator,
            Dialogs,
            Window,
            "1.2.3");
        Resolver = resolver;
    }

    public PlatformInfo Platform { get; }

    public McaccPaths Paths { get; }

    public AccountPathResolver Resolver { get; }

    public FakeAccountService Accounts { get; }

    public FakeDesktopLauncher Desktop { get; } = new();

    public FakeLoginRedirector Redirect { get; } = new();

    public FakeCodeLauncher Code { get; } = new();

    public FakeUsageProvider Usage { get; } = new();

    public UsagePoller Poller { get; }

    public FakeClaudeStatsProvider Stats { get; } = new();

    public FakeCredentialSource Credentials { get; } = new();

    public FakeLoginReader Login { get; } = new();

    public FakeDetector Detector { get; } = new();

    public FakeTerminalCatalog Terminals { get; } = new();

    public FakeShellIntegration Integration { get; } = new();

    public FakeAppLifetime Lifetime { get; } = new();

    public FakeDialogService Dialogs { get; } = new();

    public Navigator Navigator { get; }

    public MainWindowViewModel Window { get; }

    public AppServices Services { get; }

    public void Dispose()
    {
        Poller.Dispose();
        _temp.Dispose();
    }

    /// <summary>Makes the account look logged in with that e-mail.</summary>
    public void SetLogin(Account account, string email) =>
        Login.ByClaudeDir[Resolver.Resolve(account).EffectiveClaudeDir] =
            new Core.ClaudeCode.ClaudeLoginInfo(email, null, null);

    /// <summary>A running instance that the matcher will attribute to that account.</summary>
    public DesktopInstance RunningInstance(Account account, int pid = 100) =>
        new(pid, StartTime: null, Resolver.Resolve(account).DesktopUserDataDir, [pid]);
}
