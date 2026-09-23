using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Terminal;

public sealed class CodeLaunchService(
    ITerminalCatalog catalog,
    IExecutableLocator locator,
    IProcessRunner runner,
    LaunchFileWriter writer,
    AccountPathResolver resolver,
    McaccPaths paths,
    PlatformInfo platform)
{
    private const string ClaudeCli = "claude";

    public CodeLaunchResult Launch(Account account, AppSettings settings)
    {
        var installed = catalog.GetInstalled();
        var terminal = TerminalSelector.Select(settings.Terminal, installed, catalog.PlatformDefaultId);
        if (terminal is null || platform.Os is not (OsKind.Windows or OsKind.MacOS))
        {
            return new CodeLaunchResult(CodeLaunchOutcome.NoTerminal, Terminal: null, ClaudeCliNotFound: false);
        }

        var cliMissing = locator.Find(ClaudeCli) is null;
        try
        {
            var plan = Plan(Request(account), terminal, settings, installed);
            foreach (var file in plan.Files)
            {
                writer.Write(file);
            }

            runner.Start(plan.Process);
        }
        // A stale shell setting (InvalidOperationException) and an unwritable launch dir (IOException) are
        // both reported as a failed start; the user can only retry or change the setting either way.
        catch (Exception e) when (e is LaunchException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new CodeLaunchResult(CodeLaunchOutcome.StartFailed, terminal, cliMissing);
        }

        return new CodeLaunchResult(CodeLaunchOutcome.Launched, terminal, cliMissing);
    }

    private ClaudeCodeLaunchRequest Request(Account account) => new(
        account.Id,
        account.Name,
        resolver.Resolve(account).ClaudeConfigDir,
        platform.HomeDir,
        paths.LaunchDir,
        account.Color);

    private TerminalLaunchPlan Plan(
        ClaudeCodeLaunchRequest request,
        TerminalOption terminal,
        AppSettings settings,
        IReadOnlyList<TerminalOption> installed) => platform.Os == OsKind.Windows
        ? WindowsTerminalPlanner.Plan(request, terminal, settings.WindowsTerminalShell, installed)
        : MacTerminalPlanner.Plan(request, terminal);
}
