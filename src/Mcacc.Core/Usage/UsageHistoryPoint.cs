namespace Mcacc.Core.Usage;

/// <summary>One line of the limit history: the two main windows at the time of a successful refresh.</summary>
public sealed record UsageHistoryPoint(
    DateTimeOffset At,
    double? FiveHour,
    DateTimeOffset? FiveHourResetsAt,
    double? SevenDay,
    DateTimeOffset? SevenDayResetsAt)
{
    public static UsageHistoryPoint From(UsageSnapshot snapshot) => new(
        snapshot.FetchedAt,
        snapshot.FiveHour?.Utilization,
        snapshot.FiveHour?.ResetsAt,
        snapshot.SevenDay?.Utilization,
        snapshot.SevenDay?.ResetsAt);
}
