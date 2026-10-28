using System.Globalization;
using System.Text;
using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Codex;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Providers;
using Mcacc.Core.Terminal;
using Mcacc.Core.Usage;

namespace Mcacc.Platform.Common;

/// <summary>
/// A plain-text look at what MCACC sees on this machine, for the <c>--diagnose</c> switch. It shows no e-mail
/// local parts and no tokens.
/// </summary>
public static class DiagnosticsReport
{
    public static async Task<string> BuildAsync(
        PlatformServices services,
        IReadOnlyList<Account> accounts,
        CancellationToken ct)
    {
        var report = new StringBuilder();
        report.AppendLine("MCACC diagnose");
        report.AppendLine(CultureInfo.InvariantCulture, $"created   {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        AppendSystem(report, services);
        AppendInstallation(report, services);
        AppendTerminals(report, services);
        AppendCli(report, services);
        await AppendInstancesAsync(report, services, accounts, ct).ConfigureAwait(false);
        AppendAccounts(report, services, accounts);
        return report.ToString();
    }

    private static void AppendSystem(StringBuilder report, PlatformServices services)
    {
        var info = services.Info;
        report.AppendLine();
        report.AppendLine("== System ==");
        report.AppendLine(CultureInfo.InvariantCulture, $"os          {info.Os} ({Environment.OSVersion.VersionString})");
        report.AppendLine(CultureInfo.InvariantCulture, $"home        {info.HomeDir}");
        report.AppendLine(CultureInfo.InvariantCulture, $"appdata     {info.RoamingAppData}");
        report.AppendLine(CultureInfo.InvariantCulture, $"localappdata {info.LocalAppData}");
        report.AppendLine(CultureInfo.InvariantCulture, $"mcacc root  {services.Paths.Root}");
        report.AppendLine(CultureInfo.InvariantCulture, $"config      {services.Paths.ConfigFile}");
        report.AppendLine(CultureInfo.InvariantCulture, $"launch      {services.Paths.LaunchDir}");
        report.AppendLine(CultureInfo.InvariantCulture, $"logs        {services.Paths.LogsDir}");
    }

    private static void AppendInstallation(StringBuilder report, PlatformServices services)
    {
        report.AppendLine();
        report.AppendLine("== Desktop apps ==");
        foreach (var provider in ProviderDescriptors.All.Where(p => p.Desktop is not null))
        {
            var installation = services.InstallationLocator.Locate(provider);
            report.AppendLine(provider.DesktopDisplayName);
            report.AppendLine(installation is { } found
                ? $"  {found.Kind}: {found.LaunchPath}"
                : "  not installed");
        }
    }

    private static void AppendTerminals(StringBuilder report, PlatformServices services)
    {
        report.AppendLine();
        report.AppendLine("== Terminals ==");
        var installed = services.Terminals.GetInstalled();
        if (installed.Count == 0)
        {
            report.AppendLine("none found");
        }

        foreach (var terminal in installed)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"{terminal.Id,-20} {terminal.DisplayName,-20} {terminal.Path}");
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"default id   {services.Terminals.PlatformDefaultId}");
        report.AppendLine(CultureInfo.InvariantCulture,
            $"selected     {TerminalSelector.Select(null, installed, services.Terminals.PlatformDefaultId)?.Id ?? "-"}");
    }

    private static void AppendCli(StringBuilder report, PlatformServices services)
    {
        report.AppendLine();
        report.AppendLine("== Claude Code CLI ==");
        report.AppendLine(services.Executables.Find("claude") ?? "'claude' not found on the search path");
    }

    private static async Task AppendInstancesAsync(
        StringBuilder report,
        PlatformServices services,
        IReadOnlyList<Account> accounts,
        CancellationToken ct)
    {
        report.AppendLine();
        report.AppendLine("== Running instances ==");
        IReadOnlyList<DesktopInstance> instances;
        try
        {
            instances = await services.CreateDetector().DetectAsync(ProviderDescriptors.Claude, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"detection failed: {e.GetType().Name}: {e.Message}");
            return;
        }

        if (instances.Count == 0)
        {
            report.AppendLine("none");
            return;
        }

        var matcher = new DesktopInstanceMatcher(services.CreateResolver(), services.Info.PathStyle);
        foreach (var instance in instances)
        {
            var owner = matcher.Match(instance, accounts)?.Name ?? "<unknown profile>";
            report.AppendLine(CultureInfo.InvariantCulture,
                $"pid {instance.MainPid} started {instance.StartTime:yyyy-MM-dd HH:mm:ss} account '{owner}'");
            report.AppendLine(CultureInfo.InvariantCulture, $"  user-data-dir {instance.UserDataDir ?? "<app default>"}");
            report.AppendLine(CultureInfo.InvariantCulture, $"  pids          {string.Join(", ", instance.ProcessIds)}");
            if (instance.DeepLinkUrl is not null)
            {
                // Only that there is one: the URL carries a one-time login code.
                report.AppendLine("  started by a claude:// link");
            }
        }
    }

    private static void AppendAccounts(StringBuilder report, PlatformServices services, IReadOnlyList<Account> accounts)
    {
        report.AppendLine();
        report.AppendLine("== Accounts ==");
        var resolver = services.CreateResolver();
        foreach (var account in accounts)
        {
            var paths = resolver.Resolve(account);
            report.AppendLine(CultureInfo.InvariantCulture,
                $"{account.Id} '{account.Name}'{(account.IsDefault ? " (default)" : "")}");
            report.AppendLine(CultureInfo.InvariantCulture,
                $"  user-data-dir {paths.DesktopUserDataDir ?? "<app default>"}");
            report.AppendLine(CultureInfo.InvariantCulture,
                $"  config dir    {paths.ConfigDirOverride ?? "<inherited, unset>"}");
            report.AppendLine(CultureInfo.InvariantCulture, $"  provider      {paths.Provider.DisplayName}");
            report.AppendLine(CultureInfo.InvariantCulture, $"  config home   {paths.EffectiveConfigDir}");
            report.AppendLine(CultureInfo.InvariantCulture,
                $"  credentials   {(File.Exists(paths.CredentialsFile) ? "present" : "missing")}");
            report.AppendLine(CultureInfo.InvariantCulture, $"  login         {Login(account, paths)}");
            report.AppendLine(CultureInfo.InvariantCulture, $"  local limits  {LocalLimits(paths)}");
            foreach (var dir in paths.DesktopDataDirs)
            {
                report.AppendLine(CultureInfo.InvariantCulture,
                    $"  data dir      {dir} {(Directory.Exists(dir) ? "(exists)" : "(missing)")}");
            }
        }
    }

    private static readonly ILoginInfoReader LoginReaders = new RoutingLoginInfoReader(
        ProviderMap<ILoginInfoReader>.Of(new ClaudeJsonLoginInfoReader(), new CodexLoginInfoReader()));

    private static readonly CodexRateLimitsReader LocalLimitsReader = new();

    /// <summary>
    /// The limits the provider has written into its own session files, if it writes any. Claude does not, so the
    /// line stays empty there; for Codex it is the fallback the tile falls back to.
    /// </summary>
    private static string LocalLimits(AccountPaths paths)
    {
        if (LocalLimitsReader.Read(paths.EffectiveConfigDir) is not { } limits)
        {
            return "none on disk";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"5h {Percent(limits.FiveHour)} · 7d {Percent(limits.SevenDay)} · as of {limits.At:u}");
    }

    private static string Percent(UsageWindow? window) =>
        window?.Utilization is { } used ? string.Create(CultureInfo.InvariantCulture, $"{used:0.#} %") : "-";

    /// <summary>Only the domain, so a report can be shared without handing out the address.</summary>
    private static string Login(Account account, AccountPaths paths)
    {
        if (LoginReaders.Read(account, paths) is not { } info)
        {
            return "not logged in";
        }

        return $"{MaskAddresses(info.Email ?? "logged in without an address")} org '{MaskAddresses(info.OrganizationName ?? "-")}'";
    }

    /// <summary>
    /// Replaces the local part of every address in the text with an ellipsis. The default organization name is
    /// "&lt;address&gt;'s Organization", so masking the e-mail alone would still print it.
    /// </summary>
    private static string MaskAddresses(string text) => string.Join(
        ' ',
        text.Split(' ').Select(word => word.LastIndexOf('@') is var at && at > 0 ? "…" + word[at..] : word));
}
