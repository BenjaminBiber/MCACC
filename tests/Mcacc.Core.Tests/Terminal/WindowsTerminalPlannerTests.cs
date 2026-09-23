using Mcacc.Core.Model;
using Mcacc.Core.Processes;
using Mcacc.Core.Terminal;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Terminal;

public class WindowsTerminalPlannerTests
{
    private static readonly TerminalOption Wt =
        new(TerminalIds.WindowsTerminal, "Windows Terminal", @"C:\Users\x\AppData\Local\Microsoft\WindowsApps\wt.exe");
    private static readonly TerminalOption Pwsh =
        new(TerminalIds.Pwsh, "PowerShell 7", @"C:\Program Files\PowerShell\7\pwsh.exe");
    private static readonly TerminalOption WinPs =
        new(TerminalIds.WindowsPowerShell, "Windows PowerShell", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe");
    private static readonly TerminalOption Cmd =
        new(TerminalIds.Cmd, "Command Prompt", @"C:\Windows\system32\cmd.exe");

    private static readonly TerminalOption[] AllInstalled = [Wt, Pwsh, WinPs, Cmd];
    private static readonly TerminalOption[] WithoutPwsh = [Wt, WinPs, Cmd];

    private const string EscapedHome = @"C:\Users\Jö O'Neil’s 100%\;$x";
    private const string EscapedLaunchDir = @"C:\Users\Jö O'Neil’s 100%\;$x\.mcacc\launch";

    private const string ProfileScript =
        """Remove-Item Env:CLAUDECODE,Env:CLAUDE_CODE_ENTRYPOINT,Env:CLAUDE_CODE_SSE_PORT -ErrorAction Ignore; $env:CLAUDE_CONFIG_DIR='C:\Users\Jö O''Neil’’s 100%;$x\.mcacc\profiles\privat\claude'; Set-Location -LiteralPath 'C:\Users\Jö O''Neil’’s 100%;$x'; $Host.UI.RawUI.WindowTitle='Claude Code · Privat'; claude""";

    private const string EscapedProfileScript =
        """Remove-Item Env:CLAUDECODE,Env:CLAUDE_CODE_ENTRYPOINT,Env:CLAUDE_CODE_SSE_PORT -ErrorAction Ignore\; $env:CLAUDE_CONFIG_DIR='C:\Users\Jö O''Neil’’s 100%\;$x\.mcacc\profiles\privat\claude'\; Set-Location -LiteralPath 'C:\Users\Jö O''Neil’’s 100%\;$x'\; $Host.UI.RawUI.WindowTitle='Claude Code · Privat'\; claude""";

    private const string EscapedDefaultScript =
        """Remove-Item Env:CLAUDECODE,Env:CLAUDE_CODE_ENTRYPOINT,Env:CLAUDE_CODE_SSE_PORT -ErrorAction Ignore\; Remove-Item Env:CLAUDE_CONFIG_DIR -ErrorAction Ignore\; Set-Location -LiteralPath 'C:\Users\Jö O''Neil’’s 100%\;$x'\; $Host.UI.RawUI.WindowTitle='Claude Code · Standard'\; claude""";

    private static readonly LaunchFile ProfileBatchFile = new(
        LaunchRequests.WindowsLaunchDir + @"\privat.cmd",
        CmdBatchBuilder.Build(LaunchRequests.WindowsProfile()),
        LineEnding.CrLf,
        Executable: false);

    private static string[] WtNewTab(string title, string color, string escapedDirectory) =>
        ["-w", "new", "new-tab", "--title", title, "--suppressApplicationTitle", "--tabColor", color, "-d", escapedDirectory];

    private static string[] WtPowerShellArguments(string powerShellPath) =>
    [
        .. WtNewTab("Claude Code · Privat", LaunchRequests.ProfileColor, EscapedHome),
        powerShellPath, "-NoLogo", "-NoExit", "-Command", EscapedProfileScript,
    ];

    private static TerminalLaunchPlan Plan(TerminalOption terminal, WindowsTerminalShell shell, TerminalOption[] installed) =>
        WindowsTerminalPlanner.Plan(LaunchRequests.WindowsProfile(), terminal, shell, installed);

    [Fact]
    public void Windows_terminal_with_auto_opens_a_new_tab_running_pwsh_when_installed()
    {
        var plan = Plan(Wt, WindowsTerminalShell.Auto, AllInstalled);

        plan.Process.FileName.ShouldBe(Wt.Path);
        plan.Process.Arguments.ShouldBe(WtPowerShellArguments(Pwsh.Path));
        plan.Process.WorkingDirectory.ShouldBeNull();
        plan.Files.ShouldBeEmpty();
    }

    [Fact]
    public void Windows_terminal_with_auto_falls_back_to_windows_powershell_without_pwsh()
    {
        var plan = Plan(Wt, WindowsTerminalShell.Auto, WithoutPwsh);

        plan.Process.Arguments.ShouldBe(WtPowerShellArguments(WinPs.Path));
    }

    [Fact]
    public void Windows_terminal_with_pwsh_uses_pwsh_and_falls_back_to_windows_powershell_without_it()
    {
        Plan(Wt, WindowsTerminalShell.Pwsh, AllInstalled).Process.Arguments.ShouldBe(WtPowerShellArguments(Pwsh.Path));
        Plan(Wt, WindowsTerminalShell.Pwsh, WithoutPwsh).Process.Arguments.ShouldBe(WtPowerShellArguments(WinPs.Path));
    }

    [Fact]
    public void Windows_terminal_with_windows_powershell_uses_it_even_when_pwsh_is_installed()
    {
        Plan(Wt, WindowsTerminalShell.WindowsPowerShell, AllInstalled).Process.Arguments
            .ShouldBe(WtPowerShellArguments(WinPs.Path));
    }

    [Fact]
    public void Windows_terminal_with_windows_powershell_missing_uses_pwsh()
    {
        Plan(Wt, WindowsTerminalShell.WindowsPowerShell, [Wt, Pwsh, Cmd]).Process.Arguments
            .ShouldBe(WtPowerShellArguments(Pwsh.Path));
    }

    [Theory]
    [InlineData(WindowsTerminalShell.Auto)]
    [InlineData(WindowsTerminalShell.Pwsh)]
    [InlineData(WindowsTerminalShell.WindowsPowerShell)]
    public void Windows_terminal_without_any_powershell_is_refused(WindowsTerminalShell shell)
    {
        Should.Throw<InvalidOperationException>(() => Plan(Wt, shell, [Wt, Cmd]));
    }

    [Fact]
    public void Windows_terminal_escapes_semicolons_in_home_and_script_but_the_direct_shell_does_not()
    {
        var viaWt = Plan(Wt, WindowsTerminalShell.Auto, AllInstalled).Process.Arguments;
        var direct = Plan(Pwsh, WindowsTerminalShell.Auto, AllInstalled).Process.Arguments;

        viaWt[9].ShouldBe(EscapedHome);
        viaWt[^1].ShouldBe(EscapedProfileScript);
        direct[^1].ShouldBe(ProfileScript);
    }

    [Fact]
    public void Windows_terminal_for_the_default_account_removes_the_config_dir_in_the_script()
    {
        var plan = WindowsTerminalPlanner.Plan(LaunchRequests.WindowsDefault(), Wt, WindowsTerminalShell.Auto, AllInstalled);

        plan.Process.Arguments.ShouldBe(
        [
            .. WtNewTab("Claude Code · Standard", LaunchRequests.DefaultColor, EscapedHome),
            Pwsh.Path, "-NoLogo", "-NoExit", "-Command", EscapedDefaultScript,
        ]);
    }

    [Fact]
    public void Windows_terminal_with_cmd_writes_the_batch_file_and_runs_it_from_the_launch_dir()
    {
        var plan = Plan(Wt, WindowsTerminalShell.Cmd, AllInstalled);

        plan.Files.ShouldBe([ProfileBatchFile]);
        plan.Process.FileName.ShouldBe(Wt.Path);
        plan.Process.Arguments.ShouldBe(
        [
            .. WtNewTab("Claude Code · Privat", LaunchRequests.ProfileColor, EscapedLaunchDir),
            Cmd.Path, "/k", "privat.cmd",
        ]);
        plan.Process.WorkingDirectory.ShouldBeNull();
    }

    [Fact]
    public void Windows_terminal_with_cmd_missing_is_refused()
    {
        Should.Throw<InvalidOperationException>(() => Plan(Wt, WindowsTerminalShell.Cmd, [Wt, Pwsh, WinPs]));
    }

    [Theory]
    [InlineData(TerminalIds.Pwsh)]
    [InlineData(TerminalIds.WindowsPowerShell)]
    public void Direct_powershell_runs_the_unescaped_script_in_home_whatever_the_wt_shell_setting(string id)
    {
        var terminal = AllInstalled.Single(t => t.Id == id);

        var plan = Plan(terminal, WindowsTerminalShell.Cmd, AllInstalled);

        plan.Process.FileName.ShouldBe(terminal.Path);
        plan.Process.Arguments.ShouldBe(["-NoLogo", "-NoExit", "-Command", ProfileScript]);
        plan.Process.WorkingDirectory.ShouldBe(LaunchRequests.WindowsHome);
        plan.Files.ShouldBeEmpty();
    }

    [Fact]
    public void Direct_cmd_writes_the_batch_file_and_runs_it_in_the_launch_dir()
    {
        var plan = Plan(Cmd, WindowsTerminalShell.Auto, AllInstalled);

        plan.Files.ShouldBe([ProfileBatchFile]);
        plan.Process.FileName.ShouldBe(Cmd.Path);
        plan.Process.Arguments.ShouldBe(["/k", "privat.cmd"]);
        plan.Process.WorkingDirectory.ShouldBe(LaunchRequests.WindowsLaunchDir);
    }

    [Theory]
    [InlineData(TerminalIds.WindowsTerminal, WindowsTerminalShell.Auto)]
    [InlineData(TerminalIds.WindowsTerminal, WindowsTerminalShell.Cmd)]
    [InlineData(TerminalIds.Pwsh, WindowsTerminalShell.Auto)]
    [InlineData(TerminalIds.WindowsPowerShell, WindowsTerminalShell.Auto)]
    [InlineData(TerminalIds.Cmd, WindowsTerminalShell.Auto)]
    public void Every_plan_unsets_the_scrub_list_and_the_config_dir_and_sets_nothing(string id, WindowsTerminalShell shell)
    {
        var terminal = AllInstalled.Single(t => t.Id == id);

        foreach (var request in new[] { LaunchRequests.WindowsProfile(), LaunchRequests.WindowsDefault() })
        {
            var process = WindowsTerminalPlanner.Plan(request, terminal, shell, AllInstalled).Process;

            process.EnvUnset.ShouldBe(["CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_SSE_PORT", "CLAUDE_CONFIG_DIR"]);
            process.EnvSet.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Default_account_batch_file_is_named_after_its_id()
    {
        var plan = WindowsTerminalPlanner.Plan(LaunchRequests.WindowsDefault(), Cmd, WindowsTerminalShell.Auto, AllInstalled);

        plan.Files.Single().Path.ShouldBe(LaunchRequests.WindowsLaunchDir + @"\standard.cmd");
        plan.Files.Single().Content.ShouldContain("set \"CLAUDE_CONFIG_DIR=\"\r\n");
        plan.Process.Arguments.ShouldBe(["/k", "standard.cmd"]);
    }

    [Fact]
    public void A_mac_terminal_is_refused()
    {
        var terminalApp = new TerminalOption(TerminalIds.TerminalApp, "Terminal", "/System/Applications/Utilities/Terminal.app");

        Should.Throw<ArgumentException>(() => Plan(terminalApp, WindowsTerminalShell.Auto, AllInstalled));
    }

    [Theory]
    [InlineData(@"..\evil")]
    [InlineData("a b")]
    [InlineData("Privat")]
    [InlineData("")]
    public void An_account_id_that_is_not_a_slug_is_refused(string id)
    {
        var request = LaunchRequests.WindowsProfile() with { AccountId = id };

        Should.Throw<ArgumentException>(() => WindowsTerminalPlanner.Plan(request, Cmd, WindowsTerminalShell.Auto, AllInstalled));
        Should.Throw<ArgumentException>(() => WindowsTerminalPlanner.Plan(request, Wt, WindowsTerminalShell.Auto, AllInstalled));
    }
}
