namespace Mcacc.Core.Usage;

/// <param name="Utilization">Percent used, 0-100. Null when the plan reports no limit for this window.</param>
public sealed record UsageWindow(double? Utilization, DateTimeOffset? ResetsAt);
