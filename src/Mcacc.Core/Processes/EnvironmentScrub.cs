namespace Mcacc.Core.Processes;

public static class EnvironmentScrub
{
    public const string ClaudeConfigDirVariable = "CLAUDE_CONFIG_DIR";

    /// <summary>Removed from every launched process, so nothing inherits MCACC's own Claude Code session.</summary>
    public static IReadOnlyList<string> Always { get; } = ["CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_SSE_PORT"];
}
