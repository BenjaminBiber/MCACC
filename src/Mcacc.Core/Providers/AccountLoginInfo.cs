namespace Mcacc.Core.Providers;

/// <summary>Who is logged in, as the provider's own files on this machine name them.</summary>
/// <param name="Email">
/// Null when the provider is logged in without one — a Codex login with an API key has no e-mail address.
/// </param>
/// <param name="AccountUuid">Null when the files do not name it; the e-mail then identifies the account.</param>
/// <param name="OrganizationRole">The role in the organization, e.g. <c>admin</c>.</param>
/// <param name="PlanType">The plan as the provider wrote it, e.g. <c>plus</c>; null when missing.</param>
/// <param name="NumStartups">How often the CLI has started with this config; Claude only.</param>
/// <param name="InstallMethod">How the CLI was installed, e.g. <c>winget</c> or <c>native</c>; Claude only.</param>
/// <param name="ProjectCount">The number of folders the CLI remembers; Claude only.</param>
public sealed record AccountLoginInfo(
    string? Email,
    string? OrganizationName,
    string? DisplayName,
    string? AccountUuid = null,
    string? OrganizationRole = null,
    string? PlanType = null,
    int? NumStartups = null,
    string? InstallMethod = null,
    int? ProjectCount = null);
