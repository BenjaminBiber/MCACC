using System.Text.Json.Nodes;

namespace Mcacc.Core.Tests.TestSupport;

/// <summary>Lines of a Claude Code session log, built as json so paths with backslashes stay valid.</summary>
internal static class LogLines
{
    public static string Assistant(
        string id,
        string requestId = "req_1",
        string timestamp = "2026-09-20T10:15:00.000Z",
        string model = "claude-opus-4-5",
        string cwd = @"C:\Code\Mcacc",
        string session = "s1",
        long input = 10,
        long output = 20,
        long cacheRead = 0,
        long cache5m = 0,
        long cache1h = 0,
        long? legacyCacheWrite = null)
    {
        var usage = new JsonObject
        {
            ["input_tokens"] = input,
            ["output_tokens"] = output,
            ["cache_read_input_tokens"] = cacheRead,
        };
        if (legacyCacheWrite is { } legacy)
        {
            usage["cache_creation_input_tokens"] = legacy;
        }
        else
        {
            usage["cache_creation_input_tokens"] = cache5m + cache1h;
            usage["cache_creation"] = new JsonObject
            {
                ["ephemeral_5m_input_tokens"] = cache5m,
                ["ephemeral_1h_input_tokens"] = cache1h,
            };
        }

        return new JsonObject
        {
            ["type"] = "assistant",
            ["timestamp"] = timestamp,
            ["cwd"] = cwd,
            ["sessionId"] = session,
            ["requestId"] = requestId,
            ["message"] = new JsonObject
            {
                ["id"] = id,
                ["model"] = model,
                ["role"] = "assistant",
                ["usage"] = usage,
            },
        }.ToJsonString();
    }

    public static string User(string text = "hallo") => new JsonObject
    {
        ["type"] = "user",
        ["timestamp"] = "2026-09-20T10:14:00.000Z",
        ["message"] = new JsonObject { ["role"] = "user", ["content"] = text },
    }.ToJsonString();
}
