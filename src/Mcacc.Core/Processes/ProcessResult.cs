namespace Mcacc.Core.Processes;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, bool TimedOut);
