using System.Collections.Concurrent;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Desktop;

/// <summary>
/// Keeps a browser login on the profile it was started from. Claude Desktop hands the login to the system browser,
/// which comes back as a <c>claude://</c> URL; the operating system opens that with the registered handler and
/// therefore without any <c>--user-data-dir</c>, so the one-time code would be redeemed in the app default profile.
/// This watches for such a start, ends it before it can redeem the code, and starts the URL again with the profile
/// of the chosen account.
/// </summary>
public sealed class DesktopLoginRedirector(
    IDesktopInstallationLocator locator,
    IDesktopInstanceDetector detector,
    IDesktopInstanceTerminator terminator,
    IProcessRunner runner,
    AccountPathResolver resolver,
    PlatformInfo platform,
    ILog log,
    TimeProvider? time = null) : IDesktopLoginRedirector
{
    /// <summary>Long enough for a login in the browser, including a second factor.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(3);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>No grace: the stray instance must not finish booting, or it redeems the code.</summary>
    private static readonly TimeSpan QuitGrace = TimeSpan.Zero;

    /// <summary>How far before the watch an instance may have started and still count as part of this login.</summary>
    private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>The running watch of each provider. A newer one replaces it here, and the older one sees that at its next look.</summary>
    private readonly ConcurrentDictionary<ProviderKind, object> _watches = new();

    /// <exception cref="PlatformNotSupportedException">Claude Desktop is installed on neither Windows nor macOS.</exception>
    public async Task<DesktopRedirectOutcome> WatchAsync(Account target, CancellationToken ct)
    {
        // A login coming back names no account, so it can only go to the start made last: this watch ends the one
        // before it, even when it turns out to need none itself.
        var watch = new object();
        _watches[target.Provider] = watch;
        try
        {
            return await RunWatchAsync(target, watch, ct).ConfigureAwait(false);
        }
        finally
        {
            _watches.TryRemove(KeyValuePair.Create(target.Provider, watch));
        }
    }

    public bool StopWatching(ProviderKind provider) => _watches.TryRemove(provider, out _);

    private async Task<DesktopRedirectOutcome> RunWatchAsync(Account target, object watch, CancellationToken ct)
    {
        var paths = resolver.Resolve(target);
        if (paths.DesktopUserDataDir is not { } targetDir)
        {
            return DesktopRedirectOutcome.NotNeeded;
        }

        var installation = locator.Locate(paths.Provider);
        if (installation is null)
        {
            return DesktopRedirectOutcome.NotNeeded;
        }

        var since = _time.GetUtcNow() - StartTolerance;
        var started = _time.GetTimestamp();
        var targetSeen = false;
        while (_time.GetElapsedTime(started) < Window)
        {
            await Task.Delay(PollInterval, _time, ct).ConfigureAwait(false);
            var instances = await DetectAsync(paths.Provider, ct).ConfigureAwait(false);

            // Asked after the look, not before it: a snapshot taken while a newer watch began is not this one's to act on.
            if (!IsCurrent(target.Provider, watch))
            {
                return DesktopRedirectOutcome.Superseded;
            }

            if (instances is null)
            {
                continue;
            }

            if (instances.FirstOrDefault(i => IsStrayLogin(i, targetDir, since)) is { } stray)
            {
                return await HandOverAsync(installation, paths, target, stray, ct).ConfigureAwait(false);
            }

            var targetRuns = instances.Any(i => i.UserDataDir is { } dir && PathComparer.Same(dir, targetDir, platform.PathStyle));
            if (targetSeen && !targetRuns)
            {
                // Claude Desktop was closed again, so no login is on its way back.
                return DesktopRedirectOutcome.NotSeen;
            }

            targetSeen |= targetRuns;
        }

        return DesktopRedirectOutcome.NotSeen;
    }

    private bool IsCurrent(ProviderKind provider, object watch) =>
        _watches.TryGetValue(provider, out var current) && ReferenceEquals(current, watch);

    /// <returns>Null when this snapshot could not be taken; the next one may work.</returns>
    private async Task<IReadOnlyList<DesktopInstance>?> DetectAsync(ProviderDescriptor provider, CancellationToken ct)
    {
        try
        {
            return await detector.DetectAsync(provider, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.Warn("Looking for a misrouted Claude Desktop login failed.", e);
            return null;
        }
    }

    private bool IsStrayLogin(DesktopInstance instance, string targetDir, DateTimeOffset since) =>
        instance.DeepLinkUrl is not null
        && (instance.StartTime is null || instance.StartTime >= since)
        && (instance.UserDataDir is null || !PathComparer.Same(instance.UserDataDir, targetDir, platform.PathStyle));

    private async Task<DesktopRedirectOutcome> HandOverAsync(
        DesktopInstallation installation,
        AccountPaths paths,
        Account target,
        DesktopInstance stray,
        CancellationToken ct)
    {
        // The code in the URL is single use, so the stray instance has to be gone before the URL is handed on.
        await terminator.QuitAsync(stray, QuitGrace, ct).ConfigureAwait(false);
        try
        {
            runner.Start(Plan(installation, paths, stray.DeepLinkUrl!));
        }
        catch (LaunchException e)
        {
            log.Error($"Handing the Claude Desktop login to '{target.Id}' failed.", e);
            return DesktopRedirectOutcome.Failed;
        }

        // The URL itself never goes into the log: it carries the one-time login code.
        log.Info($"Claude Desktop login redirected from pid {stray.MainPid} to '{target.Id}'.");
        return DesktopRedirectOutcome.Redirected;
    }

    private ProcessSpec Plan(DesktopInstallation installation, AccountPaths paths, string deepLinkUrl) => platform.Os switch
    {
        OsKind.Windows => DesktopLaunchPlanner.PlanWindows(installation, paths, platform.HomeDir, deepLinkUrl),
        OsKind.MacOS => DesktopLaunchPlanner.PlanMac(installation, paths, deepLinkUrl),
        _ => throw new PlatformNotSupportedException($"Claude Desktop cannot be launched on {platform.Os}."),
    };
}
