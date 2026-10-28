using System.Globalization;
using System.Text.Json;
using Mcacc.Core.Usage;

namespace Mcacc.Core.Codex;

/// <summary>
/// Reads the rate limits out of either place Codex puts them: the reply of the usage endpoint, or a
/// <c>rate_limits</c> line in a session file. Unknown fields are ignored, so a new one does not break the read.
/// </summary>
public static class CodexRateLimitsParser
{
    /// <summary>300 minutes.</summary>
    private const int FiveHourMinutes = 300;

    /// <summary>10080 minutes.</summary>
    private const int SevenDayMinutes = 7 * 24 * 60;

    /// <summary>The marker a session line must contain; checking for it is far cheaper than parsing the line.</summary>
    public const string RolloutMarker = "\"rate_limits\"";

    /// <returns>Null when the body is no json object or names no window at all.</returns>
    public static CodexRateLimits? ParseLive(string json, DateTimeOffset fetchedAt)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Object(root, "rate_limit") is not { } limits)
            {
                return null;
            }

            return Build(
                limits,
                "primary_window",
                "secondary_window",
                fetchedAt,
                Text(root, "plan_type"),
                Credits(Object(root, "credits")),
                Text(limits, "rate_limit_reached_type") ?? Text(root, "rate_limit_reached_type"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <returns>Null when the line is no json object or carries no usable <c>rate_limits</c> object.</returns>
    public static CodexRateLimits? ParseRolloutLine(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // The object sits on the payload of an event line; a future shape may put it at the root.
            var payload = Object(root, "payload");
            var limits = (payload is { } inner ? Object(inner, "rate_limits") : null) ?? Object(root, "rate_limits");
            if (limits is not { } found)
            {
                return null;
            }

            var at = Timestamp(root) ?? (payload is { } p ? Timestamp(p) : null);
            return Build(
                found,
                "primary",
                "secondary",
                at ?? DateTimeOffset.UnixEpoch,
                Text(found, "plan_type"),
                Credits(Object(found, "credits")),
                Text(found, "rate_limit_reached_type"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CodexRateLimits? Build(
        JsonElement limits,
        string primaryName,
        string secondaryName,
        DateTimeOffset at,
        string? planType,
        CodexCredits? credits,
        string? limitReachedType)
    {
        UsageWindow? fiveHour = null;
        UsageWindow? sevenDay = null;
        Dictionary<string, UsageWindow>? extra = null;

        // Position decides only when the window length is missing: the reply has named the windows wrong before.
        foreach (var (name, fallback) in new[] { (primaryName, FiveHourMinutes), (secondaryName, SevenDayMinutes) })
        {
            if (Object(limits, name) is not { } element)
            {
                continue;
            }

            var window = new UsageWindow(Utilization(element), ResetsAt(element));
            switch (Minutes(element) ?? fallback)
            {
                case FiveHourMinutes:
                    fiveHour = window;
                    break;
                case SevenDayMinutes:
                    sevenDay = window;
                    break;
                default:
                    extra ??= new Dictionary<string, UsageWindow>(StringComparer.Ordinal);
                    extra[$"window_{Minutes(element)}m"] = window;
                    break;
            }
        }

        return fiveHour is null && sevenDay is null && extra is null
            ? null
            : new CodexRateLimits(fiveHour, sevenDay, at, planType, credits, limitReachedType, extra);
    }

    /// <summary>The endpoint counts the window in seconds, the session files in minutes.</summary>
    private static int? Minutes(JsonElement window)
    {
        if (Number(window, "window_minutes") is { } minutes)
        {
            return (int)minutes;
        }

        return Number(window, "limit_window_seconds") is { } seconds ? (int)(seconds / 60) : null;
    }

    private static double? Utilization(JsonElement window) => Number(window, "used_percent");

    /// <summary>Unix seconds in both spellings, unlike the Anthropic reply, which uses an iso string.</summary>
    private static DateTimeOffset? ResetsAt(JsonElement window)
    {
        var seconds = Number(window, "reset_at") ?? Number(window, "resets_at");
        if (seconds is null)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)seconds.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static CodexCredits? Credits(JsonElement? credits) => credits is { } element
        ? new CodexCredits(Flag(element, "has_credits"), Flag(element, "unlimited"), Text(element, "balance"))
        : null;

    private static DateTimeOffset? Timestamp(JsonElement owner) =>
        owner.TryGetProperty("timestamp", out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(
            value.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var timestamp)
            ? timestamp
            : null;

    private static JsonElement? Object(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static double? Number(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;

    private static bool Flag(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
