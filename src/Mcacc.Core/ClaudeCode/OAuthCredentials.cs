namespace Mcacc.Core.ClaudeCode;

/// <param name="ExpiresAt">Null when the file does not say; then the token is tried.</param>
/// <param name="SubscriptionType">The plan as Claude Code wrote it, e.g. <c>max</c>; null when missing.</param>
/// <param name="Scopes">Null when missing or empty, so two parses of the same file compare equal.</param>
public sealed record OAuthCredentials(
    string AccessToken,
    DateTimeOffset? ExpiresAt,
    string? SubscriptionType = null,
    IReadOnlyList<string>? Scopes = null)
{
    /// <summary>Keeps the token out of logs and test output; the generated record ToString would print it.</summary>
    public override string ToString() =>
        $"OAuthCredentials {{ AccessToken = <hidden>, ExpiresAt = {ExpiresAt:o}, SubscriptionType = {SubscriptionType} }}";
}
