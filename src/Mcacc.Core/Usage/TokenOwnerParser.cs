using System.Text.Json;

namespace Mcacc.Core.Usage;

/// <summary>Reads the reply of the unofficial <c>/api/oauth/profile</c> endpoint; only <c>account</c> is looked at.</summary>
public static class TokenOwnerParser
{
    /// <returns>Null when the body is not a json object or its account has neither a uuid nor an e-mail.</returns>
    public static TokenOwner? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("account", out var account)
                || account.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var uuid = Text(account, "uuid");
            var email = Text(account, "email");
            return uuid is null && email is null ? null : new TokenOwner(uuid, email);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
