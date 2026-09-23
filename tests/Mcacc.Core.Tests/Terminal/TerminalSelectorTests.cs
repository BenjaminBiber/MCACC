using Mcacc.Core.Model;
using Mcacc.Core.Terminal;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Terminal;

public class TerminalSelectorTests
{
    private static readonly TerminalOption Wt = new(TerminalIds.WindowsTerminal, "Windows Terminal", "wt.exe");
    private static readonly TerminalOption WinPs = new(TerminalIds.WindowsPowerShell, "Windows PowerShell", "powershell.exe");
    private static readonly TerminalOption Cmd = new(TerminalIds.Cmd, "Command Prompt", "cmd.exe");

    [Fact]
    public void Configured_terminal_wins_when_installed()
    {
        TerminalSelector.Select(TerminalIds.Cmd, [Wt, WinPs, Cmd], TerminalIds.WindowsTerminal).ShouldBe(Cmd);
    }

    [Fact]
    public void Configured_terminal_that_is_not_installed_falls_back_to_the_platform_default()
    {
        TerminalSelector.Select(TerminalIds.Pwsh, [WinPs, Wt, Cmd], TerminalIds.WindowsTerminal).ShouldBe(Wt);
    }

    [Fact]
    public void Nothing_configured_uses_the_platform_default()
    {
        TerminalSelector.Select(null, [WinPs, Wt, Cmd], TerminalIds.WindowsTerminal).ShouldBe(Wt);
    }

    [Fact]
    public void Missing_platform_default_falls_back_to_the_first_installed()
    {
        TerminalSelector.Select(null, [WinPs, Cmd], TerminalIds.WindowsTerminal).ShouldBe(WinPs);
        TerminalSelector.Select(TerminalIds.Pwsh, [Cmd, WinPs], TerminalIds.WindowsTerminal).ShouldBe(Cmd);
    }

    [Fact]
    public void Nothing_installed_selects_nothing()
    {
        TerminalSelector.Select(TerminalIds.Cmd, [], TerminalIds.WindowsTerminal).ShouldBeNull();
    }

    [Fact]
    public void Ids_are_matched_exactly()
    {
        TerminalSelector.Select("CMD", [Wt, Cmd], TerminalIds.WindowsTerminal).ShouldBe(Wt);
    }
}
