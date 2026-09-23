namespace Mcacc.Core.Model;

public sealed record AppSettings(
    string? Terminal = null,
    WindowsTerminalShell WindowsTerminalShell = WindowsTerminalShell.Auto,
    bool CloseAfterLaunch = true,
    string? LastAccountId = null,
    LaunchTarget? LastTarget = null);
