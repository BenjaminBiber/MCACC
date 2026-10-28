namespace Mcacc.Core.Usage;

/// <param name="Source">
/// Where the numbers came from. <see cref="UsageSource.LocalFile"/> means they are as old as
/// <paramref name="FetchedAt"/> says, not as old as the moment they were read.
/// </param>
/// <param name="Extra">
/// Further windows such as <c>seven_day_opus</c>, keyed by their API name; null when the reply has none. Null rather than
/// empty keeps the record equality of snapshots without extra windows.
/// </param>
public sealed record UsageSnapshot(
    UsageWindow? FiveHour,
    UsageWindow? SevenDay,
    DateTimeOffset FetchedAt,
    IReadOnlyDictionary<string, UsageWindow>? Extra = null,
    UsageSource Source = UsageSource.Live);
