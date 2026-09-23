namespace Mcacc.Core.Processes;

/// <param name="Files">Written before <paramref name="Process"/> is started.</param>
public sealed record TerminalLaunchPlan(ProcessSpec Process, IReadOnlyList<LaunchFile> Files);
