using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;

namespace Mcacc.Platform.Windows;

/// <summary>
/// Closes one Claude Desktop instance: first its window, then the whole process tree. It only ever touches the pids
/// of that instance, and it leaves a process alone whose start time no longer matches the detected one.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDesktopInstanceTerminator : IDesktopInstanceTerminator
{
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(5);

    public async Task<QuitResult> QuitAsync(DesktopInstance instance, TimeSpan grace, CancellationToken ct)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(instance.MainPid);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return QuitResult.Exited;
        }

        using (process)
        {
            if (IsDifferentProcess(process, instance))
            {
                return QuitResult.Exited;
            }

            try
            {
                process.CloseMainWindow();
                if (await WaitForExitAsync(process, grace, ct).ConfigureAwait(false))
                {
                    return QuitResult.Exited;
                }

                process.Kill(entireProcessTree: true);
                return await WaitForExitAsync(process, KillTimeout, ct).ConfigureAwait(false)
                    ? QuitResult.Killed
                    : QuitResult.Failed;
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // InvalidOperationException also means it exited in between, but detection decides that, not this.
                return QuitResult.Failed;
            }
        }
    }

    /// <summary>Guards against a reused pid: the start time would then differ from the detected one.</summary>
    private static bool IsDifferentProcess(Process process, DesktopInstance instance)
    {
        if (instance.StartTime is not { } detected)
        {
            return false;
        }

        try
        {
            var actual = new DateTimeOffset(process.StartTime);
            return (actual - detected).Duration() > StartTimeTolerance;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
