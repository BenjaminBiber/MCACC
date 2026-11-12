using System.Globalization;
using System.Text;
using System.Text.Json;
using Mcacc.Core.Usage;

namespace Mcacc.Core.ClaudeCode;

/// <summary>
/// The limits Claude Code last handed to its status line, kept in the account's config folder. Claude Code stores them
/// nowhere itself, so MCACC runs as the status line command and writes them down; the usage service falls back to
/// them when the endpoint gives nothing. The file keeps the <c>rate_limits</c> object as Claude Code spelled it, plus
/// the moment it was recorded — that moment, not the time of reading, is how old the numbers are.
/// </summary>
public static class ClaudeLimitsFile
{
    public const string FileName = "mcacc-limits.json";

    private const string RateLimits = "rate_limits";
    private const string RecordedAt = "recorded_at";

    /// <returns>
    /// The <c>rate_limits</c> object of a status line input with neither window missing its shape, or null when the
    /// input has none (an API-key login, or no reply yet in the session) or is not json.
    /// </returns>
    public static JsonElement? RateLimitsOf(string statusLineJson)
    {
        try
        {
            using var document = JsonDocument.Parse(statusLineJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty(RateLimits, out var limits)
                   && ToSnapshot(limits, DateTimeOffset.UnixEpoch) is not null
                ? limits.Clone()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes the limits of one status line input; an input without limits leaves the file as it is.</summary>
    /// <returns>Whether a file was written.</returns>
    public static bool Record(string configDir, string statusLineJson, DateTimeOffset now)
    {
        if (RateLimitsOf(statusLineJson) is not { } limits)
        {
            return false;
        }

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(RecordedAt, now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WritePropertyName(RateLimits);
            limits.WriteTo(writer);
            writer.WriteEndObject();
        }

        Directory.CreateDirectory(configDir);
        var file = Path.Join(configDir, FileName);

        // Several sessions of one account may answer at once, so every writer gets its own temp file.
        var tempFile = $"{file}.{Environment.ProcessId}.tmp";
        File.WriteAllBytes(tempFile, buffer.ToArray());
        File.Move(tempFile, file, overwrite: true);
        return true;
    }

    /// <returns>Null when the file is missing, unreadable or carries no window.</returns>
    public static UsageSnapshot? Read(string configDir)
    {
        string json;
        try
        {
            json = File.ReadAllText(Path.Join(configDir, FileName), Encoding.UTF8);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                   && root.TryGetProperty(RecordedAt, out var recordedAt)
                   && recordedAt.ValueKind == JsonValueKind.String
                   && DateTimeOffset.TryParse(
                       recordedAt.GetString(),
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind,
                       out var at)
                   && root.TryGetProperty(RateLimits, out var limits)
                ? ToSnapshot(limits, at)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <returns>Null unless at least one of the two windows is there.</returns>
    private static UsageSnapshot? ToSnapshot(JsonElement limits, DateTimeOffset at)
    {
        if (limits.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fiveHour = Window(limits, "five_hour");
        var sevenDay = Window(limits, "seven_day");
        return fiveHour is null && sevenDay is null
            ? null
            : new UsageSnapshot(fiveHour, sevenDay, at, Source: UsageSource.LocalFile);
    }

    private static UsageWindow? Window(JsonElement limits, string name)
    {
        if (!limits.TryGetProperty(name, out var window)
            || window.ValueKind != JsonValueKind.Object
            || !window.TryGetProperty("used_percentage", out var used)
            || used.ValueKind != JsonValueKind.Number
            || !used.TryGetDouble(out var percentage))
        {
            return null;
        }

        DateTimeOffset? resetsAt = window.TryGetProperty("resets_at", out var reset)
                                   && reset.ValueKind == JsonValueKind.Number
                                   && reset.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
        return new UsageWindow(percentage, resetsAt);
    }
}
