namespace Mcacc.Core.Usage;

/// <summary>The account an OAuth token belongs to, as the profile endpoint reports it. At least one field is set.</summary>
public sealed record TokenOwner(string? AccountUuid, string? Email);
