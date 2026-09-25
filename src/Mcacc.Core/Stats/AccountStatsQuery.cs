namespace Mcacc.Core.Stats;

public static class AccountStatsQuery
{
    private static readonly IReadOnlyDictionary<string, long> NoTokens = new Dictionary<string, long>();

    /// <param name="days">How many local days, today included.</param>
    /// <param name="projectOf">The project of a working directory; <see cref="ProjectRoot.Of"/> when null.</param>
    public static AccountStats Compute(
        IEnumerable<TokenBucket> buckets,
        DateTimeOffset now,
        int days,
        TimeZoneInfo zone,
        int topProjects = 8,
        Func<string, string>? projectOf = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        var (from, to) = Range(now, days, zone);
        var inRange = InRange(buckets, from, to, zone);

        var models = inRange
            .GroupBy(entry => entry.Bucket.Model, StringComparer.Ordinal)
            .Select(group =>
            {
                var tokens = Sum(group.Select(entry => entry.Bucket.Tokens));
                return new ModelUsage(
                    group.Key,
                    tokens,
                    group.Sum(entry => entry.Bucket.Messages),
                    CostEstimator.Estimate(group.Key, tokens));
            })
            .OrderByDescending(model => model.Tokens.Total)
            .ThenBy(model => model.Model, StringComparer.Ordinal)
            .ToList();

        var projects = inRange
            .GroupBy(entry => (projectOf ?? ProjectRoot.Of)(entry.Bucket.Project), StringComparer.Ordinal)
            .Select(group => new ProjectUsage(
                group.Key,
                group.Sum(entry => entry.Bucket.Tokens.Total),
                PricedSum(group
                    .GroupBy(entry => entry.Bucket.Model, StringComparer.Ordinal)
                    .Select(model => CostEstimator.Estimate(model.Key, Sum(model.Select(entry => entry.Bucket.Tokens))))),
                group
                    .Select(entry => ProjectRoot.WorktreeOf(entry.Bucket.Project))
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
                    .Count()))
            .OrderByDescending(project => project.Tokens)
            .ThenBy(project => project.Project, StringComparer.Ordinal)
            .Take(topProjects)
            .ToList();

        return new AccountStats(
            from,
            to,
            Sum(models.Select(model => model.Tokens)),
            models.Count == 0 ? 0m : PricedSum(models.Select(model => model.Cost)),
            models.Where(model => model.Cost is null).Sum(model => model.Tokens.Total),
            models.Sum(model => model.Messages),
            inRange
                .Select(entry => entry.Bucket.Session)
                .Where(session => session.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            inRange.Select(entry => entry.Day).Distinct().Count(),
            DailyOf(inRange, from, days),
            models,
            projects);
    }

    /// <summary>
    /// Only the tokens per day and model, for a range longer than the page computes everything for: the calendar.
    /// </summary>
    /// <param name="days">How many local days, today included.</param>
    public static IReadOnlyList<DailyUsage> Daily(
        IEnumerable<TokenBucket> buckets,
        DateTimeOffset now,
        int days,
        TimeZoneInfo zone)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        var (from, to) = Range(now, days, zone);
        return DailyOf(InRange(buckets, from, to, zone), from, days);
    }

    private static (DateOnly From, DateOnly To) Range(DateTimeOffset now, int days, TimeZoneInfo zone)
    {
        var to = LocalDay(now, zone);
        return (to.AddDays(1 - days), to);
    }

    private static List<(TokenBucket Bucket, DateOnly Day)> InRange(
        IEnumerable<TokenBucket> buckets,
        DateOnly from,
        DateOnly to,
        TimeZoneInfo zone) =>
        buckets
            .Select(bucket => (Bucket: bucket, Day: LocalDay(bucket.Hour, zone)))
            .Where(entry => entry.Day >= from && entry.Day <= to)
            .ToList();

    /// <summary>Every day of the range, the ones without tokens too.</summary>
    private static List<DailyUsage> DailyOf(List<(TokenBucket Bucket, DateOnly Day)> inRange, DateOnly from, int days)
    {
        var perDay = inRange
            .GroupBy(entry => entry.Day)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, long>)group
                    .GroupBy(entry => entry.Bucket.Model, StringComparer.Ordinal)
                    .ToDictionary(
                        model => model.Key,
                        model => model.Sum(entry => entry.Bucket.Tokens.Total),
                        StringComparer.Ordinal));
        return Enumerable.Range(0, days)
            .Select(offset => from.AddDays(offset))
            .Select(day => new DailyUsage(day, perDay.GetValueOrDefault(day) ?? NoTokens))
            .ToList();
    }

    private static DateOnly LocalDay(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    /// <summary>The sum of the known costs; null when none is known, so unpriced tokens never add up to 0.</summary>
    private static decimal? PricedSum(IEnumerable<decimal?> costs) =>
        costs.Aggregate((decimal?)null, (sum, cost) => cost is { } known ? (sum ?? 0m) + known : sum);

    private static TokenCounts Sum(IEnumerable<TokenCounts> all) =>
        all.Aggregate(TokenCounts.Zero, (sum, tokens) => sum + tokens);
}
