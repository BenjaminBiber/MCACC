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
    PlatformInfo platform) : ICodeLauncher
{
    public CodeLaunchResult Launch(Account account, AppSettings settings)
    {
        var installed = catalog.GetInstalled();
        var terminal = TerminalSelector.Select(settings.Terminal, installed, catalog.PlatformDefaultId);
        if (terminal is null || platform.Os is not (OsKind.Windows or OsKind.MacOS))
        {
            return new CodeLaunchResult(CodeLaunchOutcome.NoTerminal, Terminal: null, CliNotFound: false);
        }

        var request = Request(account);
        var cliMissing = locator.Find(request.CliCommand) is null;
        try
        {
            var plan = Plan(request, terminal, settings, installed);
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

    private CliLaunchRequest Request(Account account)
    {
        var accountPaths = resolver.Resolve(account);
        return new CliLaunchRequest(
            accountPaths.Provider,
            account.Id,
            account.Name,
            accountPaths.ConfigDirOverride,
            platform.HomeDir,
            paths.LaunchDir,
            account.Color);
    }

    private TerminalLaunchPlan Plan(
        CliLaunchRequest request,
        TerminalOption terminal,
        AppSettings settings,
        IReadOnlyList<TerminalOption> installed) => platform.Os == OsKind.Windows
        ? WindowsTerminalPlanner.Plan(request, terminal, settings.WindowsTerminalShell, installed)
        : MacTerminalPlanner.Plan(request, terminal);
}
