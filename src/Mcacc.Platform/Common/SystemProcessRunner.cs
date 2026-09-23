using System.ComponentModel;
using System.Diagnostics;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Processes;

namespace Mcacc.Platform.Common;

public sealed class SystemProcessRunner : IProcessRunner
{
    public void Start(ProcessSpec spec)
    {
        try
        {
            using var process = Process.Start(StartInfo(spec, redirect: false));
            if (process is null)
            {
                throw new LaunchException($"'{spec.FileName}' did not start a new process.");
            }
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            throw new LaunchException($"'{spec.FileName}' could not be started.", e);
        }
    }

    /// <exception cref="LaunchException">The process could not be started.</exception>
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, TimeSpan timeout, CancellationToken ct)
    {
        using var process = new Process { StartInfo = StartInfo(spec, redirect: true) };
        try
        {
            if (!process.Start())
            {
                throw new LaunchException($"'{spec.FileName}' did not start a new process.");
            }
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            throw new LaunchException($"'{spec.FileName}' could not be started.", e);
        }

        var stdOut = process.StandardOutput.ReadToEndAsync(ct);
        var stdErr = process.StandardError.ReadToEndAsync(ct);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            timedOut = true;
            Kill(process);
        }

        // After the kill the pipes close, so reading the output always finishes.
        var output = await WhenReadable(stdOut).ConfigureAwait(false);
        var error = await WhenReadable(stdErr).ConfigureAwait(false);
        return new ProcessResult(timedOut ? -1 : process.ExitCode, output, error, timedOut);
    }

    private static async Task<string> WhenReadable(Task<string> reading)
    {
        try
        {
            return await reading.ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
        {
            return "";
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            // It exited between the timeout and the kill, or the tree is gone; either way there is nothing to do.
        }
    }

    private static ProcessStartInfo StartInfo(ProcessSpec spec, bool redirect)
    {
        var info = new ProcessStartInfo(spec.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = redirect,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
            WorkingDirectory = spec.WorkingDirectory ?? "",
        };
        foreach (var argument in spec.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in spec.EnvSet)
        {
            info.Environment[name] = value;
        }

        foreach (var name in spec.EnvUnset)
        {
            info.Environment.Remove(name);
        }

        return info;
    }
}
