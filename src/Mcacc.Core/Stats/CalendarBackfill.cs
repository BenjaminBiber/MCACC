namespace Mcacc.Core.Stats;

/// <param name="Tokens">MCACC's count, or the estimate when <paramref name="Estimated"/>.</param>
/// <param name="Own">What the session logs still hold of the day.</param>
public sealed record CalendarDayUsage(DateOnly Day, long Tokens, DailyUsage Own, bool Estimated);

/// <summary>
/// Fills the days whose session logs Claude Code has cleaned up with an estimate from its own summary. That summary
/// counts differently (no cache tokens, a response once per log line), so its numbers are scaled by the typical
/// ratio between the two counts on the days both know.
/// </summary>
public static class CalendarBackfill
{
    /// <summary>Below this share of the estimate the logs of a day are taken as partly deleted.</summary>
    public const double IncompleteShare = 0.5;

    public static IReadOnlyList<CalendarDayUsage> Fill(
        IReadOnlyList<DailyUsage> own,
        IReadOnlyList<ClaudeDailyTokens> summary)
    {
        var claude = summary
            .Where(entry => entry.Tokens > 0)
            .GroupBy(entry => entry.Day)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Tokens));
        var factor = FactorOf(own, claude);
        return own
            .Select(day =>
            {
                if (factor is { } f && claude.TryGetValue(day.Day, out var tokens))
                {
                    var estimate = (long)Math.Round(tokens * f);
                    if (day.Total < estimate * IncompleteShare)
                    {
                        return new CalendarDayUsage(day.Day, estimate, day, Estimated: true);
                    }
                }

                return new CalendarDayUsage(day.Day, day.Total, day, Estimated: false);
            })
            .ToList();
    }

    /// <summary>
    /// The median of MCACC's count over Claude's on the days both have: the median, so the days whose logs are
    /// partly gone do not pull it down.
    /// </summary>
    /// <returns>Null when no day is in both; then nothing can be estimated.</returns>
    private static double? FactorOf(IReadOnlyList<DailyUsage> own, IReadOnlyDictionary<DateOnly, long> claude)
    {
        var ratios = own
            .Where(day => day.Total > 0 && claude.ContainsKey(day.Day))
            .Select(day => (double)day.Total / claude[day.Day])
            .Order()
            .ToList();
        if (ratios.Count == 0)
        {
            return null;
        }

        var middle = ratios.Count / 2;
        return ratios.Count % 2 == 1 ? ratios[middle] : (ratios[middle - 1] + ratios[middle]) / 2;
    }
}
