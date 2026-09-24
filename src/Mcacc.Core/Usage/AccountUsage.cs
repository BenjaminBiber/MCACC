namespace Mcacc.Core.Usage;

/// <param name="Snapshot">Fresh when <paramref name="State"/> is Ok, otherwise the last cached one, if any.</param>
/// <param name="TokenEmail">
/// With <see cref="UsageState.WrongAccount"/>: the e-mail of the account the token belongs to, if the endpoint named it.
/// </param>
public sealed record AccountUsage(UsageState State, UsageSnapshot? Snapshot, string? TokenEmail = null);
