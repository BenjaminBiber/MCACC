namespace Mcacc.Core.Stats;

/// <summary>The usage of one assistant response in a session log.</summary>
/// <param name="Id">
/// A hash of <c>message.id|requestId</c>. Claude Code logs a response once per content block, each time with the same
/// usage, so only the first line with an id counts.
/// </param>
public sealed record LogUsage(
    long Id,
    DateTimeOffset Timestamp,
    string Model,
    string Project,
    string Session,
    TokenCounts Tokens);
