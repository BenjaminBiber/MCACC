namespace Mcacc.Core.Processes;

/// <param name="EnvSet">Variables set on top of the inherited environment.</param>
/// <param name="EnvUnset">Variables removed from the inherited environment.</param>
public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string> EnvSet,
    IReadOnlyList<string> EnvUnset);
