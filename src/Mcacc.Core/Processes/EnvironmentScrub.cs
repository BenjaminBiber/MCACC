namespace Mcacc.Core.Processes;

public static class EnvironmentScrub
{
    public const string ClaudeConfigDirVariable = "CLAUDE_CONFIG_DIR";

    /// <summary>Removed from every launched process, so none of them thinks it runs inside a Claude Code session that started MCACC.</summary>
    public static IReadOnlyList<string> Always { get; } = ["CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_SSE_PORT"];
}
