using System.Text.Json;

namespace Mcacc.Core.ClaudeCode;

/// <summary>
/// Reads the logged-in account out of <c>.claude.json</c>. The file has keys that differ only in case, which a
/// deserializer would reject, so only <c>oauthAccount</c> is picked out of the document. MCACC never writes it.
/// </summary>
public static class ClaudeJsonLoginReader
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <returns>Null when the file is missing, unreadable, not an object or holds no e-mail address.</returns>
    public static ClaudeLoginInfo? Read(string claudeJsonPath)
    {
        try
        {
            // Claude Code keeps the file open while it runs, so it is shared for writing and deleting.
            using var stream = new FileStream(
                claudeJsonPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream, Options);
            return ReadLogin(document.RootElement);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static ClaudeLoginInfo? ReadLogin(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("oauthAccount", out var account)
            || account.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var email = Text(account, "emailAddress");
        return email is null
            ? null
            : new ClaudeLoginInfo(
                email,
                Text(account, "organizationName"),
                Text(account, "displayName"),
                Text(account, "accountUuid"));
    }

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
