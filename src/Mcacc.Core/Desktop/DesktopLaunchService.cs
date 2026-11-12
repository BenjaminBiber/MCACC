using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Desktop;

/// <param name="allowParallel">
/// Asked at every check and start: whether instances of other accounts stay open next to the one being started.
/// Null keeps one account at a time.
/// </param>
public sealed class DesktopLaunchService(
    IDesktopInstallationLocator locator,
    IDesktopInstanceDetector detector,
    IDesktopInstanceTerminator terminator,
    IProcessRunner runner,
    AccountPathResolver resolver,
    DesktopInstanceMatcher matcher,
    PlatformInfo platform,
    TimeProvider? time = null,
    Func<bool>? allowParallel = null) : IDesktopLauncher
{
    private static readonly TimeSpan WindowsQuitGrace = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MacQuitGrace = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan QuitPollTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan QuitPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan PreLaunchDelay = TimeSpan.FromMilliseconds(500);

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>Side by side, an instance of another account is no conflict: every profile holds its own lock.</summary>
    private bool AllowParallel => allowParallel?.Invoke() ?? false;

    public async Task<DesktopLaunchCheck> PrepareAsync(Account target, IReadOnlyList<Account> accounts, CancellationToken ct)
    {
        if (locator.Locate(ProviderDescriptors.For(target)) is null)
        {
            return new DesktopLaunchCheck.NotInstalled();
        }

        var (conflicts, targetRunning) = await DetectAsync(target, accounts, ct).ConfigureAwait(false);
        if (conflicts.Count > 0 && !AllowParallel)
        {
            return new DesktopLaunchCheck.Conflict(conflicts);
        }

        return targetRunning ? new DesktopLaunchCheck.RunningSameAccount() : new DesktopLaunchCheck.Ready();
    }

    /// <param name="quitConflicts">
    /// Whether instances of other accounts may be quit; they are detected again first. Side by side nothing is quit,
    /// whatever this says.
    /// </param>
    /// <exception cref="PlatformNotSupportedException">Claude Desktop is installed on neither Windows nor macOS.</exception>
    public async Task<DesktopLaunchOutcome> LaunchAsync(Account target, IReadOnlyList<Account> accounts, bool quitConflicts, CancellationToken ct)
    {
        var installation = locator.Locate(ProviderDescriptors.For(target));
        if (installation is null)
        {
            return DesktopLaunchOutcome.NotInstalled;
        }

        var plan = Plan(installation, resolver.Resolve(target));

        // Side by side there is nothing to look for: whoever runs stays, so the start does not wait on a detection.
        if (!AllowParallel)
        {
            var (conflicts, _) = await DetectAsync(target, accounts, ct).ConfigureAwait(false);
            if (conflicts.Count > 0)
            {
                if (!quitConflicts)
                {
                    return DesktopLaunchOutcome.ConflictNotResolved;
                }

                var grace = platform.Os == OsKind.MacOS ? MacQuitGrace : WindowsQuitGrace;
                foreach (var conflict in conflicts)
                {
                    // Detection decides below whether the instance is gone, whatever the terminator reports.
                    await terminator.QuitAsync(conflict.Instance, grace, ct).ConfigureAwait(false);
                }

                if (!await WaitUntilNoConflictAsync(target, accounts, ct).ConfigureAwait(false))
                {
                    return DesktopLaunchOutcome.QuitFailed;
                }
            }
        }

        await Task.Delay(PreLaunchDelay, _time, ct).ConfigureAwait(false);
        try
        {
            runner.Start(plan);
        }
        catch (LaunchException)
        {
            return DesktopLaunchOutcome.StartFailed;
        }

        return DesktopLaunchOutcome.Launched;
    }

    private ProcessSpec Plan(DesktopInstallation installation, AccountPaths paths) => platform.Os switch
    {
        OsKind.Windows => DesktopLaunchPlanner.PlanWindows(installation, paths, platform.HomeDir),
        OsKind.MacOS => DesktopLaunchPlanner.PlanMac(installation, paths),
        _ => throw new PlatformNotSupportedException($"Claude Desktop cannot be launched on {platform.Os}."),
    };

    private async Task<bool> WaitUntilNoConflictAsync(Account target, IReadOnlyList<Account> accounts, CancellationToken ct)
    {
        var started = _time.GetTimestamp();
        while ((await DetectAsync(target, accounts, ct).ConfigureAwait(false)).Conflicts.Count > 0)
        {
            if (_time.GetElapsedTime(started) >= QuitPollTimeout)
            {
                return false;
            }

            await Task.Delay(QuitPollInterval, _time, ct).ConfigureAwait(false);
        }

        return true;
    }

    /// <returns>The instances that do not belong to <paramref name="target"/>, and whether one does.</returns>
    private async Task<(List<DesktopConflict> Conflicts, bool TargetRunning)> DetectAsync(
        Account target,
        IReadOnlyList<Account> accounts,
        CancellationToken ct)
    {
        var instances = await detector.DetectAsync(ProviderDescriptors.For(target), ct).ConfigureAwait(false);
        var conflicts = new List<DesktopConflict>();
        var targetRunning = false;
        foreach (var instance in instances)
        {
            var owner = matcher.Match(instance, accounts);
            if (owner?.Id == target.Id)
            {
                targetRunning = true;
            }
            else
            {
                conflicts.Add(new DesktopConflict(instance, owner));
            }
        }

        return (conflicts, targetRunning);
    }
}
