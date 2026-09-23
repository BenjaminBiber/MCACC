namespace Mcacc.Core.Processes;

public sealed record LaunchFile(string Path, string Content, LineEnding LineEnding, bool Executable);
