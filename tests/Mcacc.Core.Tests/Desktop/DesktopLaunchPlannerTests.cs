using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Desktop;

public class DesktopLaunchPlannerTests
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private const string WindowsHome = @"C:\Users\Jö O'Neil";
    private const string MacHome = "/Users/Jö O'Neil";

    private static readonly PlatformInfo Windows =
        new(OsKind.Windows, WindowsHome, WindowsHome + @"\AppData\Roaming", WindowsHome + @"\AppData\Local");

    private static readonly PlatformInfo Mac = new(OsKind.MacOS, MacHome, "", "");

    private static readonly DesktopInstallation Msix =
        new(DesktopInstallKind.Msix, WindowsHome + @"\AppData\Local\Microsoft\WindowsApps\claude-desktop.exe");

    private static readonly DesktopInstallation Squirrel =
        new(DesktopInstallKind.Squirrel, WindowsHome + @"\AppData\Local\AnthropicClaude\claude.exe");

    private static readonly DesktopInstallation Bundle = new(DesktopInstallKind.MacBundle, "/Applications/Claude.app");

    private static AccountPaths PathsOf(Account account, PlatformInfo info) =>
        new AccountPathResolver(McaccPaths.FromEnvironment(info, _ => null), info).Resolve(account);

    private static DesktopInstallation WindowsInstallation(DesktopInstallKind kind) =>
        kind == DesktopInstallKind.Msix ? Msix : Squirrel;

    [Theory]
    [InlineData(DesktopInstallKind.Msix)]
    [InlineData(DesktopInstallKind.Squirrel)]
    public void Windows_profile_passes_its_user_data_dir_as_one_argument_and_sets_its_config_dir(DesktopInstallKind kind)
    {
        var installation = WindowsInstallation(kind);

        var spec = DesktopLaunchPlanner.PlanWindows(installation, PathsOf(Privat, Windows), WindowsHome);

        spec.FileName.ShouldBe(installation.LaunchPath);
        spec.Arguments.ShouldBe([@"--user-data-dir=C:\Users\Jö O'Neil\.mcacc\profiles\privat\desktop"]);
        spec.WorkingDirectory.ShouldBe(WindowsHome);
        spec.EnvSet.Count.ShouldBe(1);
        spec.EnvSet["CLAUDE_CONFIG_DIR"].ShouldBe(@"C:\Users\Jö O'Neil\.mcacc\profiles\privat\claude");
        spec.EnvUnset.ShouldBe(["CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_SSE_PORT"]);
    }

    [Theory]
    [InlineData(DesktopInstallKind.Msix)]
    [InlineData(DesktopInstallKind.Squirrel)]
    public void Windows_default_starts_without_arguments_and_removes_the_config_dir(DesktopInstallKind kind)
    {
        var installation = WindowsInstallation(kind);

        var spec = DesktopLaunchPlanner.PlanWindows(installation, PathsOf(Standard, Windows), WindowsHome);

        spec.FileName.ShouldBe(installation.LaunchPath);
        spec.Arguments.ShouldBeEmpty();
        spec.WorkingDirectory.ShouldBe(WindowsHome);
        spec.EnvSet.ShouldBeEmpty();
        spec.EnvUnset.ShouldBe(["CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_SSE_PORT", "CLAUDE_CONFIG_DIR"]);
    }

    [Fact]
    public void Mac_profile_opens_a_new_instance_with_its_config_dir_and_user_data_dir()
    {
        var spec = DesktopLaunchPlanner.PlanMac(Bundle, PathsOf(Privat, Mac));

        spec.FileName.ShouldBe("/usr/bin/open");
        spec.Arguments.ShouldBe(
        [
            "-n", "-a", "/Applications/Claude.app",
            "--env", "CLAUDE_CONFIG_DIR=/Users/Jö O'Neil/.mcacc/profiles/privat/claude",
            "--args", "--user-data-dir=/Users/Jö O'Neil/.mcacc/profiles/privat/desktop",
        ]);
    }

    [Fact]
    public void Mac_default_opens_a_new_instance_of_the_bundle_only()
    {
        var spec = DesktopLaunchPlanner.PlanMac(Bundle, PathsOf(Standard, Mac));

        spec.FileName.ShouldBe("/usr/bin/open");
        spec.Arguments.ShouldBe(["-n", "-a", "/Applications/Claude.app"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mac_open_passes_the_config_dir_only_through_its_arguments(bool isDefault)
    {
        var spec = DesktopLaunchPlanner.PlanMac(Bundle, PathsOf(isDefault ? Standard : Privat, Mac));

        spec.WorkingDirectory.ShouldBeNull();
        spec.EnvSet.ShouldBeEmpty();
        spec.EnvUnset.ShouldBe(["CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_SSE_PORT", "CLAUDE_CONFIG_DIR"]);
    }
}
