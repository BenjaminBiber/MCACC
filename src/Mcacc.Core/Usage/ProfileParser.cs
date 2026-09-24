using System.Globalization;
using System.Text.Json;

namespace Mcacc.Core.Usage;

/// <summary>Reads the reply of the unofficial <c>/api/oauth/profile</c> endpoint; unknown fields are ignored.</summary>
public static class ProfileParser
{
    /// <returns>Null when the body is not a json object or its account has neither a uuid nor an e-mail.</returns>
    public static AccountProfile? Parse(string json)
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
            if (uuid is null && email is null)
            {
                return null;
            }

            JsonElement? organization = root.TryGetProperty("organization", out var found) && found.ValueKind == JsonValueKind.Object
                ? found
                : null;
            string? Organization(string name) => organization is { } element ? Text(element, name) : null;

            return new AccountProfile(
                uuid,
                email,
                Text(account, "full_name"),
                Text(account, "display_name"),
                Flag(account, "has_claude_max"),
                Flag(account, "has_claude_pro"),
                Date(account, "created_at"),
                Organization("name"),
                Organization("organization_type"),
                Organization("billing_type"),
                Organization("rate_limit_tier"));
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

    private static bool? Flag(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static DateTimeOffset? Date(JsonElement owner, string name) =>
        Text(owner, name) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date
            : null;
}
