namespace Mcacc.Core.Model;

/// <param name="ExcludedProjects">
/// The projects the info page leaves out of the Claude Code statistics, for all accounts; null when none is. Null
/// rather than empty keeps the key out of <c>config.json</c> and the default settings equal to each other.
/// </param>
public sealed record AppSettings(
    string? Terminal = null,
    WindowsTerminalShell WindowsTerminalShell = WindowsTerminalShell.Auto,
    bool CloseAfterLaunch = true,
    string? LastAccountId = null,
    LaunchTarget? LastTarget = null,
    IReadOnlyList<string>? ExcludedProjects = null);
