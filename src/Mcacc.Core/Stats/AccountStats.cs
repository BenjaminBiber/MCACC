namespace Mcacc.Core.Stats;

/// <summary>The Claude Code usage of one account over a range of local days.</summary>
/// <param name="Cost">
/// USD for the priced models only; see <paramref name="UnpricedTokens"/> for the rest. Null when the range has
/// models but none of them has a price, since an unpriced token must not read as costing nothing.
/// </param>
public sealed record AccountStats(
    DateOnly From,
    DateOnly To,
    TokenCounts Tokens,
    decimal? Cost,
    long UnpricedTokens,
    int Messages,
    int Sessions,
    int ActiveDays,
    IReadOnlyList<DailyUsage> Days,
    IReadOnlyList<ModelUsage> Models,
    IReadOnlyList<ProjectUsage> Projects);

public sealed record DailyUsage(DateOnly Day, IReadOnlyDictionary<string, long> TokensByModel)
{
    public long Total => TokensByModel.Values.Sum();
}

/// <param name="Cost">Null when the model has no price.</param>
public sealed record ModelUsage(string Model, TokenCounts Tokens, int Messages, decimal? Cost);

/// <param name="Project">The repository; the sessions in its worktrees count to it.</param>
/// <param name="Cost">USD for the priced models of the project; null when none of them has a price.</param>
/// <param name="Worktrees">How many of its worktrees had sessions in the range.</param>
public sealed record ProjectUsage(string Project, long Tokens, decimal? Cost, int Worktrees = 0);
