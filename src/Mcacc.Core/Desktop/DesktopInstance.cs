namespace Mcacc.Core.Desktop;

/// <param name="UserDataDir">Null means the instance runs without a <c>--user-data-dir</c> flag.</param>
/// <param name="ProcessIds">The main process first, then its Desktop descendants by ascending pid.</param>
public sealed record DesktopInstance(int MainPid, DateTimeOffset? StartTime, string? UserDataDir, IReadOnlyList<int> ProcessIds);
