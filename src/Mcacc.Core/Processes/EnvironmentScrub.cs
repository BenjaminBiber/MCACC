using Mcacc.Core.Model;

namespace Mcacc.Core.Processes;

public static class EnvironmentScrub
{
    /// <summary>
    /// Removed from every launched process, so none of them thinks it runs inside the session that started MCACC,
    /// and so no provider's state leaks out of the profile it belongs to. Both providers' markers go, whichever one
    /// is being launched: MCACC may well have been started from the other one.
    /// </summary>
    /// <remarks>
    /// <c>CODEX_SQLITE_HOME</c> is the quiet one: Codex reports its sqlite home independently of
    /// <c>CODEX_HOME</c>, so an inherited value would put a profile's databases in the default home while its
    /// configuration sits in the profile — half-isolated, and nearly invisible.
    /// </remarks>
    public static IReadOnlyList<string> Always { get; } =
    [
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
    ];

    /// <summary>Every provider's config-dir variable. A launch sets at most one of them and unsets the rest.</summary>
    public static IReadOnlyList<string> ConfigDirVariables { get; } =
        [.. ProviderDescriptors.All.Select(p => p.Cli.ConfigDirVariable)];

    /// <summary><see cref="Always"/> plus the config-dir variable of every provider but this one.</summary>
    public static IReadOnlyList<string> For(ProviderDescriptor provider) =>
        [.. Always, .. ConfigDirVariables.Where(name => name != provider.Cli.ConfigDirVariable)];

    /// <summary><see cref="Always"/> plus every config-dir variable: for a launch that sets none of them.</summary>
    public static IReadOnlyList<string> WithoutAnyConfigDir { get; } = [.. Always, .. ConfigDirVariables];
}
