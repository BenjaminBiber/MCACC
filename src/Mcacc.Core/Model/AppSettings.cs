namespace Mcacc.Core.Model;

/// <param name="ExcludedProjects">
/// The projects the info page leaves out of the Claude Code statistics, for all accounts; null when none is. Null
/// rather than empty keeps the key out of <c>config.json</c> and the default settings equal to each other.
/// </param>
/// <param name="LaunchAtLogin">MCACC starts hidden in the tray when the user logs in, so the limit history keeps growing.</param>
/// <param name="AllowParallelDesktop">
/// A desktop app may run with several accounts side by side. Off, a start quits the instance of another account
/// after asking; that stays the default, because the apps are built to run once.
/// </param>
public sealed record AppSettings(
    string? Terminal = null,
    WindowsTerminalShell WindowsTerminalShell = WindowsTerminalShell.Auto,
    bool CloseAfterLaunch = true,
    string? LastAccountId = null,
    LaunchTarget? LastTarget = null,
    IReadOnlyList<string>? ExcludedProjects = null,
    bool LaunchAtLogin = true,
    bool AllowParallelDesktop = false);
