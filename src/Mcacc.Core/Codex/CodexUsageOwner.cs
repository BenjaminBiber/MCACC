using System.Text.Json;

namespace Mcacc.Core.Codex;

/// <summary>Who the usage endpoint says the token belongs to.</summary>
public sealed record CodexUsageOwner(string? AccountUuid, string? Email, string? PlanType);

public static class CodexUsageOwnerParser
{
    /// <returns>Null when the body names neither an account nor an e-mail.</returns>
    public static CodexUsageOwner? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var accountId = Text(root, "account_id");
            var email = Text(root, "email");
            return accountId is null && email is null
                ? null
                : new CodexUsageOwner(accountId, email, Text(root, "plan_type"));
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
