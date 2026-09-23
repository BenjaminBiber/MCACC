namespace Mcacc.Core.Usage;

/// <param name="Snapshot">Fresh when <paramref name="State"/> is Ok, otherwise the last cached one, if any.</param>
public sealed record AccountUsage(UsageState State, UsageSnapshot? Snapshot);
