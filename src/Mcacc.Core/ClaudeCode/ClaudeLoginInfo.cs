namespace Mcacc.Core.ClaudeCode;

/// <param name="AccountUuid">Null when <c>.claude.json</c> does not name it; the e-mail then identifies the account.</param>
public sealed record ClaudeLoginInfo(string Email, string? OrganizationName, string? DisplayName, string? AccountUuid = null);
