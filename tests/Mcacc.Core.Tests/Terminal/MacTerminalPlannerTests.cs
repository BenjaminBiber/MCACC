using Mcacc.Core.Model;
using Mcacc.Core.Processes;
using Mcacc.Core.Terminal;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Terminal;

public class MacTerminalPlannerTests
{
    private static readonly TerminalOption TerminalApp =
        new(TerminalIds.TerminalApp, "Terminal", "/System/Applications/Utilities/Terminal.app");
    private static readonly TerminalOption ITerm =
        new(TerminalIds.ITerm2, "iTerm2", "/Applications/iTerm.app");
    private static readonly TerminalOption Ghostty =
        new(TerminalIds.Ghostty, "Ghostty", LaunchRequests.PosixHome + "/Applications/Ghostty.app");

    private const string CommandFile = LaunchRequests.PosixLaunchDir + "/privat.command";

    private static TerminalOption ById(string id) => new[] { TerminalApp, ITerm, Ghostty }.Single(t => t.Id == id);

    [Fact]
    public void Terminal_app_opens_the_command_file_with_terminal()
    {
        var plan = MacTerminalPlanner.Plan(LaunchRequests.PosixProfile(), TerminalApp);

        plan.Process.FileName.ShouldBe("/usr/bin/open");
        plan.Process.Arguments.ShouldBe(["-a", "Terminal", CommandFile]);
    }

    [Fact]
    public void Iterm2_opens_the_command_file_with_its_bundle()
    {
        var plan = MacTerminalPlanner.Plan(LaunchRequests.PosixProfile(), ITerm);

        plan.Process.FileName.ShouldBe("/usr/bin/open");
        plan.Process.Arguments.ShouldBe(["-a", "/Applications/iTerm.app", CommandFile]);
    }

    [Fact]
    public void Ghostty_opens_a_new_instance_that_executes_the_command_file()
    {
        var plan = MacTerminalPlanner.Plan(LaunchRequests.PosixProfile(), Ghostty);

        plan.Process.FileName.ShouldBe("/usr/bin/open");
        plan.Process.Arguments.ShouldBe(["-na", Ghostty.Path, "--args", "-e", CommandFile]);
    }

    [Theory]
    [InlineData(TerminalIds.TerminalApp)]
    [InlineData(TerminalIds.ITerm2)]
    [InlineData(TerminalIds.Ghostty)]
    public void Every_plan_writes_an_executable_lf_command_file_and_scrubs_the_environment(string id)
    {
        var request = LaunchRequests.PosixProfile();

        var plan = MacTerminalPlanner.Plan(request, ById(id));

        plan.Files.ShouldBe([new LaunchFile(CommandFile, PosixCommandFileBuilder.Build(request), LineEnding.Lf, Executable: true)]);
        plan.Process.EnvUnset.ShouldBe(EnvironmentScrub.WithoutAnyConfigDir);
        plan.Process.EnvSet.ShouldBeEmpty();
        plan.Process.WorkingDirectory.ShouldBeNull();
    }

    [Fact]
    public void Default_account_file_is_named_after_its_id_and_unsets_the_config_dir()
    {
        var plan = MacTerminalPlanner.Plan(LaunchRequests.PosixDefault(), TerminalApp);

        var file = plan.Files.Single();
        file.Path.ShouldBe(LaunchRequests.PosixLaunchDir + "/standard.command");
        file.Content.Split('\n').ShouldContain("unset CLAUDE_CONFIG_DIR");
        plan.Process.Arguments.ShouldBe(["-a", "Terminal", file.Path]);
    }

    [Fact]
    public void A_windows_terminal_is_refused()
    {
        var wt = new TerminalOption(TerminalIds.WindowsTerminal, "Windows Terminal", "wt.exe");

        Should.Throw<ArgumentException>(() => MacTerminalPlanner.Plan(LaunchRequests.PosixProfile(), wt));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a b")]
    [InlineData("Privat")]
    [InlineData("")]
    public void An_account_id_that_is_not_a_slug_is_refused(string id)
    {
        var request = LaunchRequests.PosixProfile() with { AccountId = id };

        Should.Throw<ArgumentException>(() => MacTerminalPlanner.Plan(request, TerminalApp));
    }
}
