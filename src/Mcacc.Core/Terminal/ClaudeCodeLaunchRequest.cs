using Mcacc.Core.Quoting;

namespace Mcacc.Core.Terminal;

/// <param name="ClaudeConfigDir">Null means <c>CLAUDE_CONFIG_DIR</c> must be unset (default account).</param>
public sealed record ClaudeCodeLaunchRequest(
    string AccountId,
    string AccountName,
    string? ClaudeConfigDir,
    string HomeDir,
    string LaunchDir,
    string TabColor)
{
    public string Title => TitleSanitizer.Sanitize($"Claude Code · {AccountName}");
}
