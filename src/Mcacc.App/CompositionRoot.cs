using System.Reflection;
using Mcacc.App.Services;
using Mcacc.App.ViewModels;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Accounts;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Codex;
using Mcacc.Core.Config;
using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Desktop;
using Mcacc.Core.Logging;
using Mcacc.Core.Processes;
using Mcacc.Core.Providers;
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

        // One implementation per provider behind each seam; the routers keep every consumer on a single interface.
        var claudeLogin = new ClaudeJsonLoginInfoReader();
        var login = new RoutingLoginInfoReader(
            ProviderMap<ILoginInfoReader>.Of(claudeLogin, new CodexLoginInfoReader()));
        var credentials = new RoutingCredentialSource(
            ProviderMap<ICredentialSource>.Of(platform.Credentials, new CodexCredentialSource()));
        var navigator = new Navigator();
        var shell = new MainWindowViewModel(navigator);
        var dialogs = new DialogService(shell);
        if (accounts.LoadResult.IsReadOnly)
        {
            shell.ShowError(Localization.Texts.ErrorConfigReadOnly);
        }

        var claudeUsage = new UsageService(
            platform.Credentials,
            claudeLogin,
            platform.UsageHttp,
            new UsageCache(platform.Paths),
            new ProfileCache(platform.Paths),
            new UsageHistoryStore(platform.Paths, log: log),
            resolver,
            log: log);
        var codexUsage = new CodexUsageService(
            platform.CodexUsageHttp,
            new CodexRateLimitsReader(),
            new UsageCache(platform.Paths),
            new ProfileCache(platform.Paths),
            new UsageHistoryStore(platform.Paths, log: log),
            resolver,
            log: log);
        var usage = new RoutingUsageProvider(ProviderMap<IUsageProvider>.Of(claudeUsage, codexUsage));
        var statsIndex = new StatsIndexStore(platform.Paths);
        var stats = new RoutingStatsProvider(ProviderMap<IStatsProvider>.Of(
            new ClaudeStatsProvider(statsIndex, resolver),
            new CodexStatsProvider(statsIndex, resolver)));
        var poller = new UsagePoller(accounts, usage, log);

        var loginItems = LoginItemSync.ForProcess(platform.LoginItem, log);
        loginItems.Apply(accounts.Config.Settings.LaunchAtLogin);

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
            stats,
            credentials,
            login,
            platform.CreateDetector(),
            new DesktopInstanceMatcher(resolver, platform.Info.PathStyle),
            platform.Terminals,
            resolver,
            platform.Paths,
            platform.Info,
            platform.Shell,
            lifetime,
            loginItems,
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
