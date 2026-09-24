namespace Mcacc.Core.ClaudeCode;

/// <param name="AccountUuid">Null when <c>.claude.json</c> does not name it; the e-mail then identifies the account.</param>
/// <param name="OrganizationRole">The role in the organization, e.g. <c>admin</c>.</param>
/// <param name="NumStartups">How often Claude Code has started with this config.</param>
/// <param name="InstallMethod">How Claude Code was installed, e.g. <c>winget</c> or <c>native</c>.</param>
/// <param name="ProjectCount">The number of folders Claude Code remembers.</param>
public sealed record ClaudeLoginInfo(
    string Email,
    string? OrganizationName,
    string? DisplayName,
    string? AccountUuid = null,
    string? OrganizationRole = null,
    int? NumStartups = null,
    string? InstallMethod = null,
    int? ProjectCount = null);
