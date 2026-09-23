namespace Mcacc.Core.Processes;

public sealed record ProcessInfo(int Pid, int ParentPid, string? ExePath, string? CommandLine, DateTimeOffset? StartTime);
