using Mcacc.Core.Providers;

namespace Mcacc.Core.Codex;

/// <summary>
/// What <c>auth.json</c> holds. Codex keeps identity and token in the same file, so one read serves both the
/// credential source and the login reader.
/// </summary>
/// <param name="AccessToken">Null for a login with an API key, which carries no OAuth token.</param>
/// <param name="HasApiKey">Whether <c>OPENAI_API_KEY</c> is set, i.e. logged in without ChatGPT.</param>
/// <param name="AccessTokenExpiresAt">From the token's own <c>exp</c> claim; null when it does not say.</param>
/// <param name="Login">From the <c>id_token</c> claims; null when there is none or it cannot be read.</param>
public sealed record CodexAuth(
    string? AccessToken,
    bool HasApiKey,
    string? AccountId,
    string? AuthMode,
    DateTimeOffset? AccessTokenExpiresAt,
    AccountLoginInfo? Login)
{
    /// <summary>Keeps the token out of logs and test output; the generated record ToString would print it.</summary>
    public override string ToString() =>
        $"CodexAuth {{ AccessToken = {(AccessToken is null ? "<none>" : "<hidden>")}, HasApiKey = {HasApiKey}, "
        + $"AccountId = {AccountId}, AuthMode = {AuthMode}, AccessTokenExpiresAt = {AccessTokenExpiresAt:o} }}";
}
