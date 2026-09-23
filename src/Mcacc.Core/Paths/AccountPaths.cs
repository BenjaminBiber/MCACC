namespace Mcacc.Core.Paths;

/// <param name="DesktopUserDataDir">Null means the app default, i.e. no <c>--user-data-dir</c> flag.</param>
/// <param name="ClaudeConfigDir">Null means <c>CLAUDE_CONFIG_DIR</c> must be unset.</param>
/// <param name="DesktopDataDirs">Candidate real Desktop data locations in preference order.</param>
public sealed record AccountPaths(
    string? DesktopUserDataDir,
    string? ClaudeConfigDir,
    string EffectiveClaudeDir,
    string ClaudeJsonFile,
    string CredentialsFile,
    IReadOnlyList<string> DesktopDataDirs);
