using System.Globalization;
using System.Text.Json;

namespace Mcacc.Core.Usage;

/// <summary>Reads the reply of the unofficial <c>/api/oauth/usage</c> endpoint; unknown fields are ignored.</summary>
public static class UsageResponseParser
{
    /// <returns>Null when the body is not a json object.</returns>
    public static UsageSnapshot? Parse(string json, DateTimeOffset fetchedAt)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind != JsonValueKind.Object
                ? null
                : new UsageSnapshot(Window(root, "five_hour"), Window(root, "seven_day"), fetchedAt, Extra(root));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static UsageWindow? Window(JsonElement root, string name) =>
        root.TryGetProperty(name, out var window) && window.ValueKind == JsonValueKind.Object
            ? new UsageWindow(Utilization(window), ResetsAt(window))
            : null;

    private static double? Utilization(JsonElement window) =>
        window.TryGetProperty("utilization", out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var utilization)
            ? utilization
            : null;

    private static DateTimeOffset? ResetsAt(JsonElement window) =>
        window.TryGetProperty("resets_at", out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resetsAt)
            ? resetsAt
            : null;

    private const string SevenDayPrefix = "seven_day_";

    /// <summary>Every <c>seven_day_*</c> object besides <c>seven_day</c>; a null window means the plan has none.</summary>
    private static Dictionary<string, UsageWindow>? Extra(JsonElement root)
    {
        Dictionary<string, UsageWindow>? extra = null;
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.StartsWith(SevenDayPrefix, StringComparison.Ordinal)
                && property.Value.ValueKind == JsonValueKind.Object)
            {
                extra ??= new Dictionary<string, UsageWindow>(StringComparer.Ordinal);
                extra[property.Name] = new UsageWindow(Utilization(property.Value), ResetsAt(property.Value));
            }
        }

        return extra;
    }
}
