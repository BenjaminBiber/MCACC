using System.Reflection;
using Mcacc.App.Services;
using Mcacc.App.ViewModels;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Accounts;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Config;
using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Desktop;
using Mcacc.Core.Logging;
using Mcacc.Core.Processes;
using Mcacc.Core.Stats;
using Mcacc.Core.Terminal;
using Mcacc.Core.Usage;
using Mcacc.Platform;

namespace Mcacc.App;

/// <summary>Wires everything by hand; MCACC is small enough that a container would only hide the graph.</summary>
public static class CompositionRoot
{
    public static (MainWindowViewModel Shell, AppServices Services) Create(PlatformServices platform, IAppLifetime lifetime)
    {
        var log = new FileLog(platform.Paths);
        var resolver = platform.CreateResolver();
        var accounts = new AccountService(
            new ConfigStore(platform.Paths),
            platform.Paths,
            resolver,
            new ConfigCopier(),
            new ProfileDataDeleter());
        LogLoad(log, accounts);

        var login = new ClaudeJsonLoginInfoReader();
        var navigator = new Navigator();
        var shell = new MainWindowViewModel(navigator);
        var dialogs = new DialogService(shell);
        if (accounts.LoadResult.IsReadOnly)
        {
            shell.ShowError(Localization.Texts.ErrorConfigReadOnly);
        }

        var usage = new UsageService(
            platform.Credentials,
            login,
            platform.UsageHttp,
            new UsageCache(platform.Paths),
            new ProfileCache(platform.Paths),
            new UsageHistoryStore(platform.Paths, log: log),
            resolver,
            log: log);
        var poller = new UsagePoller(accounts, usage, log);

        var services = new AppServices(
            accounts,
            new DesktopLaunchService(
                platform.InstallationLocator,
                platform.CreateDetector(),
                platform.Terminator,
                platform.Runner,
                resolver,
                new DesktopInstanceMatcher(resolver, platform.Info.PathStyle),
                platform.Info),
            new DesktopLoginRedirector(
                platform.InstallationLocator,
                platform.CreateDetector(),
                platform.Terminator,
                platform.Runner,
                resolver,
                platform.Info,
                log),
            new CodeLaunchService(
                platform.Terminals,
                platform.Executables,
                platform.Runner,
                new LaunchFileWriter(),
                resolver,
                platform.Paths,
                platform.Info),
            usage,
            poller,
            new ClaudeStatsProvider(new StatsIndexStore(platform.Paths), resolver),
            platform.Credentials,
            login,
            platform.CreateDetector(),
            new DesktopInstanceMatcher(resolver, platform.Info.PathStyle),
            platform.Terminals,
            resolver,
            platform.Paths,
            platform.Info,
            platform.Shell,
            lifetime,
            log,
            navigator,
            dialogs,
            shell,
            Version());

        navigator.Navigated += page => Activate(page, log);
        navigator.Reset(new AccountPickerViewModel(services));
        poller.Start();
        return (shell, services);
    }

    public static string Version() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(fieldCount: 3) ?? "0.0.0";

    /// <summary>Activation runs in the background; a page that cannot load its extras still shows.</summary>
    private static void Activate(PageViewModel page, ILog log) => _ = ActivateAsync(page, log);

    private static async Task ActivateAsync(PageViewModel page, ILog log)
    {
        try
        {
            await page.ActivateAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.Warn($"Activating {page.GetType().Name} failed.", e);
        }
    }

    private static void LogLoad(ILog log, AccountService accounts)
    {
        var result = accounts.LoadResult;
        log.Info($"Config loaded: {result.Status}, read-only: {result.IsReadOnly}, accounts: {result.Config.Accounts.Count}");
        foreach (var repair in result.Repairs)
        {
            log.Warn($"Config repaired: {repair}");
        }
    }
}
