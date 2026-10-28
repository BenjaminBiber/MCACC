using System.Globalization;
using System.Text.Json;
using Mcacc.Core.Stats;

namespace Mcacc.Core.Codex;

/// <summary>
/// Reads the token usage out of a Codex session file. Two things differ from Claude and both of them matter:
/// <list type="bullet">
/// <item>
/// The model and the working directory are not on the usage line. They stand on the <c>session_meta</c> and
/// <c>turn_context</c> lines before it, so the reader carries them forward — which is why it keeps state.
/// </item>
/// <item>
/// <c>input_tokens</c> already includes <c>cached_input_tokens</c> and <c>cache_write_input_tokens</c>, where
/// Anthropic counts them beside it. Adding them up the Claude way would roughly double every session.
/// </item>
/// </list>
/// </summary>
public sealed class CodexRolloutLineReader : ILogLineReader
{
    private const string UsageType = "token_usage_record";
    private const string Unknown = "unknown";

    private static readonly byte[] UsageMarker = "\"usage\""u8.ToArray();
    private static readonly byte[] ContextMarker = "\"cwd\""u8.ToArray();

    public LogReaderState State { get; set; } = LogReaderState.None;

    public bool TryRead(ReadOnlyMemory<byte> line, out LogUsage? usage)
    {
        usage = null;

        // Most lines are messages and tool output; neither marker is in them, so they cost one scan, not a parse.
        var span = line.Span;
        if (span.IndexOf(UsageMarker) < 0 && span.IndexOf(ContextMarker) < 0)
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Object(root, "payload") is not { } payload)
            {
                return true;
            }

            RememberContext(root, payload);
            usage = Usage(root, payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>A turn names the model and the directory once; every usage line after it belongs to them.</summary>
    private void RememberContext(JsonElement root, JsonElement payload)
    {
        var type = Text(root, "type");
        if (type is not ("session_meta" or "turn_context"))
        {
            return;
        }

        // session_meta keeps them one level deeper, on its own payload.
        var model = Text(payload, "model") ?? Text(root, "model");
        var cwd = Text(payload, "cwd") ?? Text(root, "cwd");
        State = new LogReaderState(model ?? State.Model, cwd ?? State.Project);
    }

    private LogUsage? Usage(JsonElement root, JsonElement payload)
    {
        if (Text(root, "type") != UsageType
            || Object(payload, "usage") is not { } counts
            || Text(payload, "response_id") is not { } responseId
            || Timestamp(root) is not { } timestamp)
        {
            return null;
        }

        var tokens = Tokens(counts);
        if (tokens.Total == 0)
        {
            return null;
        }

        return new LogUsage(
            LogLineParser.Hash(responseId),
            timestamp,
            State.Model ?? Unknown,
            State.Project ?? "",
            Text(payload, "thread_id") ?? "",
            tokens);
    }

    /// <summary>
    /// The cached and written parts are carved out of <c>input_tokens</c>, so the sum stays
    /// <c>total_tokens</c> and nothing is counted twice.
    /// </summary>
    private static TokenCounts Tokens(JsonElement usage)
    {
        var input = Number(usage, "input_tokens");
        var cached = Number(usage, "cached_input_tokens");
        var written = Number(usage, "cache_write_input_tokens");
        return new TokenCounts(
            Input: Math.Max(0, input - cached - written),
            Output: Number(usage, "output_tokens"),
            CacheWrite5Min: written,
            CacheWrite1Hour: 0,
            CacheRead: cached);
    }

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

    private static long Number(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : 0;

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
