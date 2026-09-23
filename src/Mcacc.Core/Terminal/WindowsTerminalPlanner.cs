using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;
using Mcacc.Core.Quoting;

namespace Mcacc.Core.Terminal;

public static class WindowsTerminalPlanner
{
    /// <param name="shell">Only used when <paramref name="terminal"/> is Windows Terminal.</param>
    /// <param name="installed">Where Windows Terminal finds the shell it runs.</param>
    /// <exception cref="ArgumentException">Not a Windows terminal, or the account id is not a slug.</exception>
    /// <exception cref="InvalidOperationException">The shell Windows Terminal should run is not installed.</exception>
    public static TerminalLaunchPlan Plan(
        ClaudeCodeLaunchRequest req,
        TerminalOption terminal,
        WindowsTerminalShell shell,
        IReadOnlyList<TerminalOption> installed)
    {
        TerminalPlanning.RequireSlugAccountId(req);
        return terminal.Id switch
        {
            TerminalIds.WindowsTerminal when shell == WindowsTerminalShell.Cmd =>
                WindowsTerminalCmd(req, terminal, InstalledCmd(installed)),
            TerminalIds.WindowsTerminal =>
                WindowsTerminalPowerShell(req, terminal, InstalledPowerShell(shell, installed)),
            TerminalIds.Pwsh or TerminalIds.WindowsPowerShell => new TerminalLaunchPlan(
                TerminalPlanning.Process(terminal.Path, PowerShellArguments(PowerShellScriptBuilder.Build(req)), req.HomeDir),
                []),
            TerminalIds.Cmd => new TerminalLaunchPlan(
                TerminalPlanning.Process(terminal.Path, CmdArguments(req), req.LaunchDir),
                [BatchFile(req)]),
            _ => throw new ArgumentException($"'{terminal.Id}' is not a Windows terminal.", nameof(terminal)),
        };
    }

    private static TerminalLaunchPlan WindowsTerminalPowerShell(ClaudeCodeLaunchRequest req, TerminalOption wt, TerminalOption powerShell)
    {
        var script = WtEscaping.EscapeSemicolons(PowerShellScriptBuilder.Build(req));
        string[] arguments = [.. NewTab(req, req.HomeDir), powerShell.Path, .. PowerShellArguments(script)];
        return new TerminalLaunchPlan(TerminalPlanning.Process(wt.Path, arguments, workingDirectory: null), []);
    }

    private static TerminalLaunchPlan WindowsTerminalCmd(ClaudeCodeLaunchRequest req, TerminalOption wt, TerminalOption cmd)
    {
        string[] arguments = [.. NewTab(req, req.LaunchDir), cmd.Path, .. CmdArguments(req)];
        return new TerminalLaunchPlan(TerminalPlanning.Process(wt.Path, arguments, workingDirectory: null), [BatchFile(req)]);
    }

    private static string[] NewTab(ClaudeCodeLaunchRequest req, string directory) =>
    [
        "-w", "new", "new-tab",
        "--title", req.Title,
        "--suppressApplicationTitle",
        "--tabColor", req.TabColor,
        "-d", WtEscaping.EscapeSemicolons(directory),
    ];

    private static string[] PowerShellArguments(string script) => ["-NoLogo", "-NoExit", "-Command", script];

    private static string[] CmdArguments(ClaudeCodeLaunchRequest req) => ["/k", BatchFileName(req)];

    private static string BatchFileName(ClaudeCodeLaunchRequest req) => $"{req.AccountId}.cmd";

    private static LaunchFile BatchFile(ClaudeCodeLaunchRequest req) => new(
        PathStyle.Windows.Join(req.LaunchDir, BatchFileName(req)),
        CmdBatchBuilder.Build(req),
        LineEnding.CrLf,
        Executable: false);

    private static TerminalOption InstalledCmd(IReadOnlyList<TerminalOption> installed) =>
        Installed(installed, TerminalIds.Cmd)
        ?? throw new InvalidOperationException("The command prompt is not installed.");

    private static TerminalOption InstalledPowerShell(WindowsTerminalShell shell, IReadOnlyList<TerminalOption> installed)
    {
        string[] preference = shell switch
        {
            WindowsTerminalShell.Auto or WindowsTerminalShell.Pwsh => [TerminalIds.Pwsh, TerminalIds.WindowsPowerShell],
            WindowsTerminalShell.WindowsPowerShell => [TerminalIds.WindowsPowerShell, TerminalIds.Pwsh],
            _ => throw new ArgumentOutOfRangeException(nameof(shell), shell, null),
        };
        return preference.Select(id => Installed(installed, id)).FirstOrDefault(option => option is not null)
            ?? throw new InvalidOperationException("Neither PowerShell 7 nor Windows PowerShell is installed.");
    }

    private static TerminalOption? Installed(IReadOnlyList<TerminalOption> installed, string id) =>
        installed.FirstOrDefault(option => option.Id == id);
}
