namespace Mcacc.Core.Terminal;

/// <param name="Terminal">The selected terminal, null only when none is installed.</param>
/// <param name="ClaudeCliNotFound">A warning: the login shell may still find <c>claude</c>.</param>
public sealed record CodeLaunchResult(CodeLaunchOutcome Outcome, TerminalOption? Terminal, bool ClaudeCliNotFound);
