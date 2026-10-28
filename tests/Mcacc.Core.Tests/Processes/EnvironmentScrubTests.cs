using Mcacc.Core.Model;
using Mcacc.Core.Processes;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Processes;

/// <summary>The one place the scrub list is pinned by name; the script tests derive theirs from it.</summary>
public class EnvironmentScrubTests
{
    [Fact]
    public void Always_drops_both_providers_session_markers()
    {
        EnvironmentScrub.Always.ShouldBe([
            "CLAUDECODE",
            "CLAUDE_CODE_ENTRYPOINT",
            "CLAUDE_CODE_SSE_PORT",
            "CODEX_SQLITE_HOME",
            "CODEX_ACCESS_TOKEN",
            "CODEX_SESSION_ID",
            "CODEX_SANDBOX",
            "CODEX_SANDBOX_NETWORK_DISABLED",
            "CODEX_NON_INTERACTIVE",
            "CODEX_PERMISSION_PROFILE",
            "CODEX_INTERNAL_ORIGINATOR_OVERRIDE",
            "CODEX_MANAGED_PACKAGE_ROOT",
            "CODEX_ROLLOUT_TRACE_ROOT",
            "CODEX_DAEMON_SHUTDOWN_FILE",
            "CODEX_WINDOWS_REGISTERED_CORE",
            "CODEX_ELECTRON_USER_DATA_PATH",
        ]);
    }

    /// <summary>
    /// Codex reports its sqlite home independently of CODEX_HOME, so an inherited value would leave a profile's
    /// databases in the default home — isolated configuration, shared state.
    /// </summary>
    [Fact]
    public void Always_drops_the_Codex_sqlite_home()
    {
        EnvironmentScrub.Always.ShouldContain("CODEX_SQLITE_HOME");
    }

    [Fact]
    public void Every_providers_config_dir_variable_is_known()
    {
        EnvironmentScrub.ConfigDirVariables.ShouldBe(["CLAUDE_CONFIG_DIR", "CODEX_HOME"]);
    }

    /// <summary>MCACC may have been started from the other provider's session, so its variable must go too.</summary>
    [Theory]
    [InlineData(ProviderKind.Claude, "CLAUDE_CONFIG_DIR", "CODEX_HOME")]
    [InlineData(ProviderKind.Codex, "CODEX_HOME", "CLAUDE_CONFIG_DIR")]
    public void For_keeps_the_launched_providers_variable_and_drops_the_others(
        ProviderKind kind,
        string own,
        string foreign)
    {
        var scrub = EnvironmentScrub.For(ProviderDescriptors.For(kind));

        scrub.ShouldNotContain(own);
        scrub.ShouldContain(foreign);
        scrub.Take(EnvironmentScrub.Always.Count).ShouldBe(EnvironmentScrub.Always);
    }

    [Fact]
    public void WithoutAnyConfigDir_drops_every_config_dir_variable()
    {
        EnvironmentScrub.WithoutAnyConfigDir.ShouldBe([.. EnvironmentScrub.Always, "CLAUDE_CONFIG_DIR", "CODEX_HOME"]);
    }
}
