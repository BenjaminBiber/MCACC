using Mcacc.Core.Usage;

namespace Mcacc.Core.Codex;

/// <param name="HasCredits">Whether the account has pay-as-you-go credits at all.</param>
/// <param name="Balance">As the reply spells it — a string, not a number.</param>
public sealed record CodexCredits(bool HasCredits, bool Unlimited, string? Balance);

/// <summary>
/// Codex reports the same two windows as Claude does, but in two spellings: the endpoint counts the window in
/// seconds, the session files in minutes. Both are mapped by window length, never by position.
/// </summary>
/// <param name="At">
/// When the numbers were true. For a value out of a session file this is the line's own timestamp, not the moment
/// it was read — otherwise a week-old number would look fresh.
/// </param>
/// <param name="LimitReachedType">Non-null when the reply says a limit has been hit.</param>
/// <param name="Extra">Windows of a length MCACC does not know, keyed <c>window_&lt;minutes&gt;m</c>.</param>
public sealed record CodexRateLimits(
    UsageWindow? FiveHour,
    UsageWindow? SevenDay,
    DateTimeOffset At,
    string? PlanType = null,
    CodexCredits? Credits = null,
    string? LimitReachedType = null,
    IReadOnlyDictionary<string, UsageWindow>? Extra = null)
{
    public UsageSnapshot ToSnapshot(UsageSource source) => new(FiveHour, SevenDay, At, Extra, source);
}
