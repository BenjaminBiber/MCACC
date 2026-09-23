using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Processes;

namespace Mcacc.Platform.Windows;

/// <summary>
/// Lists every <c>claude.exe</c>. WMI can hang for seconds, so the query runs on a background task the caller stops
/// waiting for after <see cref="QueryTimeout"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WmiProcessSnapshotProvider : IProcessSnapshotProvider
{
    private const string Query =
        "SELECT ProcessId,ParentProcessId,ExecutablePath,CommandLine,CreationDate FROM Win32_Process WHERE Name='claude.exe'";

    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(3);

    /// <exception cref="TimeoutException">WMI did not answer within three seconds.</exception>
    /// <exception cref="ManagementException">The query failed.</exception>
    public async Task<IReadOnlyList<ProcessInfo>> GetClaudeProcessesAsync(CancellationToken ct)
    {
        var query = Task.Run(Read, ct);
        var finished = await Task.WhenAny(query, Task.Delay(QueryTimeout, ct)).ConfigureAwait(false);
        if (finished != query)
        {
            ct.ThrowIfCancellationRequested();
            throw new TimeoutException("WMI did not answer within three seconds.");
        }

        return await query.ConfigureAwait(false);
    }

    private static IReadOnlyList<ProcessInfo> Read()
    {
        using var searcher = new ManagementObjectSearcher(Query);
        using var results = searcher.Get();
        var processes = new List<ProcessInfo>();
        foreach (var row in results)
        {
            using var process = (ManagementObject)row;
            processes.Add(new ProcessInfo(
                Number(process, "ProcessId"),
                Number(process, "ParentProcessId"),
                Text(process, "ExecutablePath"),
                Text(process, "CommandLine"),
                StartTime(process)));
        }

        return processes;
    }

    private static int Number(ManagementObject process, string name) =>
        process[name] is { } value ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;

    private static string? Text(ManagementObject process, string name) => process[name] as string;

    private static DateTimeOffset? StartTime(ManagementObject process)
    {
        if (process["CreationDate"] is not string created || created.Length == 0)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(ManagementDateTimeConverter.ToDateTime(created));
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            return null;
        }
    }
}
