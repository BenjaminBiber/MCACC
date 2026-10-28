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

    /// <param name="own">All of MCACC's days; they decide which days are incomplete and the factor.</param>
    /// <param name="counted">
    /// The same days without the excluded projects, which is what the calendar shows; <paramref name="own"/> when
    /// null. Leaving a project out must not make a day look deleted, and the summary cannot leave it out, so an
    /// estimate is scaled by the share the counted projects have on the days with logs.
    /// </param>
    public static IReadOnlyList<CalendarDayUsage> Fill(
        IReadOnlyList<DailyUsage> own,
        IReadOnlyList<DailyTokens> summary,
        IReadOnlyList<DailyUsage>? counted = null)
    {
        counted ??= own;
        if (counted.Count != own.Count)
        {
            throw new ArgumentException("The counted days must be the same days as the own ones.", nameof(counted));
        }

        var claude = summary
            .Where(entry => entry.Tokens > 0)
            .GroupBy(entry => entry.Day)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Tokens));
        var factor = FactorOf(own, claude);
        var share = ShareOf(own, counted);
        return own
            .Select((day, index) =>
            {
                var shown = counted[index];
                if (factor is { } f && claude.TryGetValue(day.Day, out var tokens))
                {
                    var estimate = (long)Math.Round(tokens * f);
                    if (day.Total < estimate * IncompleteShare)
                    {
                        return new CalendarDayUsage(day.Day, (long)Math.Round(estimate * share), shown, Estimated: true);
                    }
                }

                return new CalendarDayUsage(day.Day, shown.Total, shown, Estimated: false);
            })
            .ToList();
    }

    /// <returns>The counted tokens over all tokens of the days with logs; 1 without any.</returns>
    private static double ShareOf(IReadOnlyList<DailyUsage> own, IReadOnlyList<DailyUsage> counted)
    {
        var all = own.Sum(day => day.Total);
        return all > 0 ? (double)counted.Sum(day => day.Total) / all : 1;
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
