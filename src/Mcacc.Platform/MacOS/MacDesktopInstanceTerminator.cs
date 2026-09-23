using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Processes;

namespace Mcacc.Platform.MacOS;

/// <summary>
/// Closes one Claude Desktop instance with SIGTERM and, if that is not enough, SIGKILL. It only ever signals the
/// pids of that instance, and it leaves a pid alone whose start time no longer matches the detected one.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacDesktopInstanceTerminator(IProcessRunner runner) : IDesktopInstanceTerminator
{
    private const string Ps = "/bin/ps";

    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PsTimeout = TimeSpan.FromSeconds(5);

    public async Task<QuitResult> QuitAsync(DesktopInstance instance, TimeSpan grace, CancellationToken ct)
    {
        if (!IsRunning(instance.MainPid))
        {
            return QuitResult.Exited;
        }

        if (await IsDifferentProcessAsync(instance, ct).ConfigureAwait(false))
        {
            return QuitResult.Exited;
        }

        if (LibC.Kill(instance.MainPid, LibC.Sigterm) != 0 && Errno() != LibC.Esrch)
        {
            return QuitResult.Failed;
        }

        if (await WaitUntilGoneAsync([instance.MainPid], grace, ct).ConfigureAwait(false))
        {
            return QuitResult.Exited;
        }

        foreach (var pid in instance.ProcessIds)
        {
            LibC.Kill(pid, LibC.Sigkill);
        }

        return await WaitUntilGoneAsync(instance.ProcessIds, KillTimeout, ct).ConfigureAwait(false)
            ? QuitResult.Killed
            : QuitResult.Failed;
    }

    private static int Errno() => Marshal.GetLastPInvokeError();

    /// <summary>Signal 0 asks the kernel whether the pid exists without touching the process.</summary>
    private static bool IsRunning(int pid) => LibC.Kill(pid, 0) == 0 || Errno() != LibC.Esrch;

    private async Task<bool> WaitUntilGoneAsync(IReadOnlyList<int> pids, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (pids.Any(IsRunning))
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Guards against a reused pid: the start time would then differ from the detected one.</summary>
    private async Task<bool> IsDifferentProcessAsync(DesktopInstance instance, CancellationToken ct)
    {
        if (instance.StartTime is not { } detected)
        {
            return false;
        }

        var spec = new ProcessSpec(
            Ps,
            ["-o", "lstart=", "-p", instance.MainPid.ToString(CultureInfo.InvariantCulture)],
            WorkingDirectory: null,
            ReadOnlyDictionary<string, string>.Empty,
            EnvironmentScrub.Always);
        ProcessResult result;
        try
        {
            result = await runner.RunAsync(spec, PsTimeout, ct).ConfigureAwait(false);
        }
        catch (LaunchException)
        {
            return false;
        }

        // An unreadable start time is no reason to spare the process; only a different one is.
        return PsOutputParser.ParseStartTime(result.StdOut) is { } actual
               && (actual - detected).Duration() > StartTimeTolerance;
    }
}
