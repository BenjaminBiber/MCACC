using System.Text.Json;
using Mcacc.Core.Providers;

namespace Mcacc.Core.Codex;

/// <summary>Reads <c>&lt;codexHome&gt;/auth.json</c>. MCACC never writes it and never refreshes a token.</summary>
public static class CodexAuthParser
{
    private const string OpenAiAuthClaim = "https://api.openai.com/auth";

    /// <returns>Null when the json is not an object, or names neither a token nor an API key.</returns>
    public static CodexAuth? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var tokens = Object(root, "tokens");
            var accessToken = tokens is { } withToken ? Text(withToken, "access_token") : null;
            var hasApiKey = Text(root, "OPENAI_API_KEY") is not null;
            if (accessToken is null && !hasApiKey)
            {
                return null;
            }

            return new CodexAuth(
                accessToken,
                hasApiKey,
                tokens is { } withId ? Text(withId, "account_id") : null,
                Text(root, "auth_mode"),
                ExpiresAt(accessToken),
                tokens is { } withIdToken ? Login(Text(withIdToken, "id_token")) : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The access token is itself a JWT; its expiry saves asking an endpoint with a dead token.</summary>
    private static DateTimeOffset? ExpiresAt(string? accessToken)
    {
        using var payload = JwtPayload.Read(accessToken);
        if (payload is null
            || !payload.RootElement.TryGetProperty("exp", out var exp)
            || exp.ValueKind != JsonValueKind.Number
            || !exp.TryGetInt64(out var seconds))
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <returns>Null when the token is unreadable or names neither an e-mail nor an account.</returns>
    private static AccountLoginInfo? Login(string? idToken)
    {
        using var payload = JwtPayload.Read(idToken);
        if (payload is null || payload.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var root = payload.RootElement;
        var auth = Object(root, OpenAiAuthClaim);
        var email = Text(root, "email");
        var accountId = auth is { } withAccount ? Text(withAccount, "chatgpt_account_id") : null;
        if (email is null && accountId is null)
        {
            return null;
        }

        var organization = auth is { } withOrganizations ? DefaultOrganization(withOrganizations) : null;
        return new AccountLoginInfo(
            email,
            OrganizationName: organization is { } named ? Text(named, "title") : null,
            DisplayName: Text(root, "name"),
            AccountUuid: accountId,
            OrganizationRole: organization is { } withRole ? Text(withRole, "role") : null,
            PlanType: auth is { } withPlan ? Text(withPlan, "chatgpt_plan_type") : null);
    }

    /// <summary>The organization marked as the default, or the only one when none is marked.</summary>
    private static JsonElement? DefaultOrganization(JsonElement auth)
    {
        if (!auth.TryGetProperty("organizations", out var organizations)
            || organizations.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? only = null;
        var count = 0;
        foreach (var organization in organizations.EnumerateArray())
        {
            if (organization.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            count++;
            only ??= organization;
            if (organization.TryGetProperty("is_default", out var isDefault)
                && isDefault.ValueKind == JsonValueKind.True)
            {
                return organization;
            }
        }

        return count == 1 ? only : null;
    }

    private static JsonElement? Object(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
