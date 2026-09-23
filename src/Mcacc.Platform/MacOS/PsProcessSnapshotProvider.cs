using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Processes;

namespace Mcacc.Platform.MacOS;

/// <summary>
/// Lists the processes of the Claude Desktop bundle. Unlike Windows there is no name to filter on, so everything is
/// listed and only the bundle's own processes are kept.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class PsProcessSnapshotProvider(IProcessRunner runner) : IProcessSnapshotProvider
{
    private const string Ps = "/bin/ps";
    private const string BundleMarker = "Claude.app/Contents/";

    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(5);

    /// <exception cref="TimeoutException">ps did not answer within five seconds.</exception>
    /// <exception cref="LaunchException">ps could not be started.</exception>
    public async Task<IReadOnlyList<ProcessInfo>> GetClaudeProcessesAsync(CancellationToken ct)
    {
        var spec = new ProcessSpec(
            Ps,
            ["-axww", "-o", "pid=,ppid=,lstart=,command="],
            WorkingDirectory: null,
            ReadOnlyDictionary<string, string>.Empty,
            EnvironmentScrub.Always);
        var result = await runner.RunAsync(spec, QueryTimeout, ct).ConfigureAwait(false);
        if (result.TimedOut)
        {
            throw new TimeoutException("ps did not answer within five seconds.");
        }

        return [.. PsOutputParser.Parse(result.StdOut)
            .Where(p => p.CommandLine?.Contains(BundleMarker, StringComparison.Ordinal) == true)];
    }
}
