using System.Globalization;
using System.Text.Json;

namespace Mcacc.Core.Stats;

/// <summary>Reads one line of a Claude Code session log; only assistant lines with a usage block count.</summary>
public static class LogLineParser
{
    private static ReadOnlySpan<byte> UsageMarker => "\"usage\""u8;

    /// <param name="usage">The usage of the line, or null when it holds none.</param>
    /// <returns>False only for a line that mentions usage but is not valid json, e.g. one cut off mid-write.</returns>
    public static bool TryParse(ReadOnlyMemory<byte> line, out LogUsage? usage)
    {
        usage = null;

        // Most lines are user turns and tool results; they are skipped without parsing.
        if (line.Span.IndexOf(UsageMarker) < 0)
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            usage = Read(document.RootElement);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>FNV-1a over the UTF-16 code units: stable across runs, unlike <see cref="string.GetHashCode()"/>.</summary>
    public static long Hash(string text)
    {
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offsetBasis;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= prime;
        }

        return unchecked((long)hash);
    }

    private static LogUsage? Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || Text(root, "type") != "assistant"
            || !root.TryGetProperty("message", out var message)
            || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("usage", out var usage)
            || usage.ValueKind != JsonValueKind.Object
            || Text(message, "id") is not { } messageId
            || Timestamp(root) is not { } timestamp)
        {
            return null;
        }

        // Synthetic messages (model "<synthetic>") carry zeros; they would only add a model without tokens.
        var tokens = Tokens(usage);
        if (tokens.Total == 0)
        {
            return null;
        }

        return new LogUsage(
            Hash($"{messageId}|{Text(root, "requestId")}"),
            timestamp,
            Text(message, "model") ?? "unknown",
            Text(root, "cwd") ?? "",
            Text(root, "sessionId") ?? "",
            tokens);
    }

    private static TokenCounts Tokens(JsonElement usage)
    {
        long cache5Min;
        long cache1Hour;
        if (usage.TryGetProperty("cache_creation", out var creation)
            && creation.ValueKind == JsonValueKind.Object
            && (creation.TryGetProperty("ephemeral_5m_input_tokens", out _)
                || creation.TryGetProperty("ephemeral_1h_input_tokens", out _)))
        {
            cache5Min = Number(creation, "ephemeral_5m_input_tokens");
            cache1Hour = Number(creation, "ephemeral_1h_input_tokens");
        }
        else
        {
            // Older logs have no split; five minutes was the only lifetime then.
            cache5Min = Number(usage, "cache_creation_input_tokens");
            cache1Hour = 0;
        }

        return new TokenCounts(
            Number(usage, "input_tokens"),
            Number(usage, "output_tokens"),
            cache5Min,
            cache1Hour,
            Number(usage, "cache_read_input_tokens"));
    }

    private static long Number(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
        && number > 0
            ? number
            : 0;

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static DateTimeOffset? Timestamp(JsonElement root) =>
        Text(root, "timestamp") is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;
}
