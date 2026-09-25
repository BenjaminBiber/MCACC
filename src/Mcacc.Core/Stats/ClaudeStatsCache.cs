using System.Globalization;
using System.Text.Json;

namespace Mcacc.Core.Stats;

/// <param name="Tokens">In Claude's own count: input and output only, a response once per log line it spans.</param>
public sealed record ClaudeDailyTokens(DateOnly Day, long Tokens);

/// <summary>
/// Reads the daily summary Claude Code keeps in <c>stats-cache.json</c> for its <c>/stats</c> view. It outlives the
/// session logs Claude Code cleans up, so it knows days the logs no longer do.
/// </summary>
public static class ClaudeStatsCache
{
    public const string FileName = "stats-cache.json";

    /// <returns>The days with tokens, oldest first; empty when the file is missing or unreadable.</returns>
    public static IReadOnlyList<ClaudeDailyTokens> Read(string claudeDir)
    {
        try
        {
            using var stream = File.OpenRead(Path.Join(claudeDir, FileName));
            using var document = JsonDocument.Parse(stream);
            return Parse(document.RootElement);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<ClaudeDailyTokens> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("dailyModelTokens", out var days)
            || days.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<ClaudeDailyTokens>();
        foreach (var entry in days.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("date", out var date)
                || date.ValueKind != JsonValueKind.String
                || !DateOnly.TryParseExact(date.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                || !entry.TryGetProperty("tokensByModel", out var models)
                || models.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var tokens = models.EnumerateObject()
                .Where(model => model.Value.ValueKind == JsonValueKind.Number && model.Value.TryGetInt64(out _))
                .Sum(model => Math.Max(0, model.Value.GetInt64()));
            if (tokens > 0)
            {
                result.Add(new ClaudeDailyTokens(day, tokens));
            }
        }

        return result.OrderBy(entry => entry.Day).ToList();
    }
}
