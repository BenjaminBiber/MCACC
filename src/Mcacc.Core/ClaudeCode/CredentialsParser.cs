using System.Text.Json;
using System.Linq;

namespace Mcacc.Core.ClaudeCode;

public static class CredentialsParser
{
    /// <returns>Null when the json holds no non-empty access token.</returns>
    public static OAuthCredentials? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
                || oauth.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!oauth.TryGetProperty("accessToken", out var token)
                || token.ValueKind != JsonValueKind.String
                || token.GetString() is not { Length: > 0 } accessToken)
            {
                return null;
            }

            return new OAuthCredentials(accessToken, ExpiresAt(oauth), SubscriptionType(oauth), Scopes(oauth));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <returns>Null when <c>expiresAt</c> is missing or not a number of milliseconds since the epoch.</returns>
    private static DateTimeOffset? ExpiresAt(JsonElement oauth)
    {
        if (!oauth.TryGetProperty("expiresAt", out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out var milliseconds))
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? SubscriptionType(JsonElement oauth) =>
        oauth.TryGetProperty("subscriptionType", out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static List<string>? Scopes(JsonElement oauth)
    {
        if (!oauth.TryGetProperty("scopes", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var scopes = value.EnumerateArray()
            .Where(scope => scope.ValueKind == JsonValueKind.String)
            .Select(scope => scope.GetString()!)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToList();
        return scopes.Count > 0 ? scopes : null;
    }
}
