using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Terminal;

public static class MacTerminalPlanner
{
    private const string Open = "/usr/bin/open";

    /// <exception cref="ArgumentException">Not a macOS terminal, or the account id is not a slug.</exception>
    public static TerminalLaunchPlan Plan(CliLaunchRequest req, TerminalOption terminal)
    {
        TerminalPlanning.RequireSlugAccountId(req);
        var file = PathStyle.Posix.Join(req.LaunchDir, $"{req.AccountId}.command");
        string[] arguments = terminal.Id switch
        {
            TerminalIds.TerminalApp => ["-a", "Terminal", file],
            TerminalIds.ITerm2 => ["-a", terminal.Path, file],
            TerminalIds.Ghostty => ["-na", terminal.Path, "--args", "-e", file],
            _ => throw new ArgumentException($"'{terminal.Id}' is not a macOS terminal.", nameof(terminal)),
        };
        return new TerminalLaunchPlan(
            TerminalPlanning.Process(Open, arguments, workingDirectory: null),
            [new LaunchFile(file, PosixCommandFileBuilder.Build(req), LineEnding.Lf, Executable: true)]);
    }
}
