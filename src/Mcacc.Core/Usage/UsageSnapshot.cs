namespace Mcacc.Core.Usage;

public sealed record UsageSnapshot(UsageWindow? FiveHour, UsageWindow? SevenDay, DateTimeOffset FetchedAt);
