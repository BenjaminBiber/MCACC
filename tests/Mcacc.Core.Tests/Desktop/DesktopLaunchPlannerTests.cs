using Mcacc.Core.Processes;
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

    private const string LoginUrl = "claude://login/google-auth?code=4%2F0AXlqoi45&hop_nonce=1BkdtqPC";

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
        spec.EnvUnset.ShouldBe(EnvironmentScrub.For(ProviderDescriptors.Claude));
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
        spec.EnvUnset.ShouldBe(EnvironmentScrub.WithoutAnyConfigDir);
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
    [InlineData(DesktopInstallKind.Msix)]
    [InlineData(DesktopInstallKind.Squirrel)]
    public void Windows_login_url_comes_behind_the_user_data_dir_it_belongs_to(DesktopInstallKind kind)
    {
        var spec = DesktopLaunchPlanner.PlanWindows(
            WindowsInstallation(kind),
            PathsOf(Privat, Windows),
            WindowsHome,
            LoginUrl);

        spec.Arguments.ShouldBe([@"--user-data-dir=C:\Users\Jö O'Neil\.mcacc\profiles\privat\desktop", LoginUrl]);
    }

    [Fact]
    public void Windows_default_takes_a_login_url_as_its_only_argument()
    {
        var spec = DesktopLaunchPlanner.PlanWindows(Msix, PathsOf(Standard, Windows), WindowsHome, LoginUrl);

        spec.Arguments.ShouldBe([LoginUrl]);
    }

    [Fact]
    public void Mac_login_url_goes_behind_the_user_data_dir_in_the_app_arguments()
    {
        var spec = DesktopLaunchPlanner.PlanMac(Bundle, PathsOf(Privat, Mac), LoginUrl);

        spec.Arguments.ShouldBe(
        [
            "-n", "-a", "/Applications/Claude.app",
            "--env", "CLAUDE_CONFIG_DIR=/Users/Jö O'Neil/.mcacc/profiles/privat/claude",
            "--args", "--user-data-dir=/Users/Jö O'Neil/.mcacc/profiles/privat/desktop", LoginUrl,
        ]);
    }

    [Fact]
    public void Mac_default_with_a_login_url_still_opens_the_args_section_for_it()
    {
        var spec = DesktopLaunchPlanner.PlanMac(Bundle, PathsOf(Standard, Mac), LoginUrl);

        spec.Arguments.ShouldBe(["-n", "-a", "/Applications/Claude.app", "--args", LoginUrl]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mac_open_passes_the_config_dir_only_through_its_arguments(bool isDefault)
    {
        var spec = DesktopLaunchPlanner.PlanMac(Bundle, PathsOf(isDefault ? Standard : Privat, Mac));

        spec.WorkingDirectory.ShouldBeNull();
        spec.EnvSet.ShouldBeEmpty();
        spec.EnvUnset.ShouldBe(EnvironmentScrub.WithoutAnyConfigDir);
    }

    private static readonly Account CodexPrivat =
        new("codex-privat", "Codex Privat", "#5E8C8A", IsDefault: false, ProviderKind.Codex);

    private static readonly DesktopInstallation CodexMsix = new(
        DesktopInstallKind.Msix,
        @"C:\Program Files\WindowsApps\OpenAI.Codex_26.930.3930.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe");

    /// <summary>
    /// Verified by starting it: the Codex app only takes a profile when all three agree — its agent home, its
    /// Electron directory in the environment, and the Chromium switch.
    /// </summary>
    [Fact]
    public void A_Codex_profile_is_named_three_times()
    {
        var paths = PathsOf(CodexPrivat, Windows);

        var spec = DesktopLaunchPlanner.PlanWindows(CodexMsix, paths, WindowsHome);

        spec.EnvSet["CODEX_HOME"].ShouldBe(paths.ConfigDirOverride);
        spec.EnvSet["CODEX_ELECTRON_USER_DATA_PATH"].ShouldBe(paths.DesktopUserDataDir);
        spec.Arguments.ShouldBe([$"--user-data-dir={paths.DesktopUserDataDir}"]);
    }

    /// <summary>Claude Desktop takes the switch alone; naming a variable it does not read would be noise.</summary>
    [Fact]
    public void A_Claude_profile_is_named_without_an_electron_variable()
    {
        var paths = PathsOf(Privat, Windows);

        var spec = DesktopLaunchPlanner.PlanWindows(Msix, paths, WindowsHome);

        spec.EnvSet.Keys.ShouldBe(["CLAUDE_CONFIG_DIR"]);
    }

    [Fact]
    public void A_Codex_start_scrubs_the_other_providers_config_dir()
    {
        var spec = DesktopLaunchPlanner.PlanWindows(CodexMsix, PathsOf(CodexPrivat, Windows), WindowsHome);

        spec.EnvUnset.ShouldContain("CLAUDE_CONFIG_DIR");
        spec.EnvUnset.ShouldContain("CODEX_SQLITE_HOME");
        spec.EnvUnset.ShouldNotContain("CODEX_HOME");
    }

    [Fact]
    public void A_Codex_start_on_mac_hands_both_variables_to_open()
    {
        var paths = PathsOf(CodexPrivat, Mac);

        var spec = DesktopLaunchPlanner.PlanMac(new DesktopInstallation(DesktopInstallKind.MacBundle, "/Applications/ChatGPT.app"), paths);

        spec.Arguments.ShouldContain($"CODEX_HOME={paths.ConfigDirOverride}");
        spec.Arguments.ShouldContain($"CODEX_ELECTRON_USER_DATA_PATH={paths.DesktopUserDataDir}");
        spec.Arguments.ShouldContain($"--user-data-dir={paths.DesktopUserDataDir}");
    }

    /// <summary>The account on the provider's own home sets nothing and has every config dir removed.</summary>
    [Fact]
    public void The_Codex_default_account_sets_no_variable_at_all()
    {
        var spec = DesktopLaunchPlanner.PlanWindows(
            CodexMsix,
            PathsOf(Account.CreateDefault(ProviderKind.Codex), Windows),
            WindowsHome);

        spec.EnvSet.ShouldBeEmpty();
        spec.Arguments.ShouldBeEmpty();
        spec.EnvUnset.ShouldContain("CODEX_HOME");
        spec.EnvUnset.ShouldContain("CLAUDE_CONFIG_DIR");
    }

    [Fact]
    public void A_Codex_deep_link_comes_after_the_profile_switch()
    {
        const string url = "codex://login/app-google-auth?code=abc";

        var spec = DesktopLaunchPlanner.PlanWindows(CodexMsix, PathsOf(CodexPrivat, Windows), WindowsHome, url);

        spec.Arguments[^1].ShouldBe(url);
        spec.Arguments[0].ShouldStartWith("--user-data-dir=");
    }
}
