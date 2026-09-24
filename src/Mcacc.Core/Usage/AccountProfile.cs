namespace Mcacc.Core.Usage;

/// <summary>
/// What <c>/api/oauth/profile</c> says about the owner of a token. A null field was not in the reply.
/// At least the uuid or the e-mail is set.
/// </summary>
public sealed record AccountProfile(
    string? AccountUuid,
    string? Email,
    string? FullName,
    string? DisplayName,
    bool? HasClaudeMax,
    bool? HasClaudePro,
    DateTimeOffset? CreatedAt,
    string? OrganizationName,
    string? OrganizationType,
    string? BillingType,
    string? RateLimitTier)
{
    public TokenOwner ToOwner() => new(AccountUuid, Email);
}
