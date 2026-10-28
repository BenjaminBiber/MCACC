using System.Text;
using System.Text.Json;
using Mcacc.Core.Codex;
using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Codex;

/// <summary>
/// Runs the reader over a real <c>sessions</c> tree instead of a fixture, because the one mistake that matters here
/// — counting the cached tokens twice — only shows against real numbers. Opt in like the screenshots do: set
/// MCACC_REAL_CODEX to a Codex home.
/// </summary>
[Trait("Category", "Integration")]
public class CodexRealSessionsTests
{
    private static string? CodexHome => Environment.GetEnvironmentVariable("MCACC_REAL_CODEX");

    /// <summary>Codex keeps the file of a running session open, so a plain read would fail on it.</summary>
    private static IEnumerable<string> SharedLines(FileInfo file)
    {
        using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static IEnumerable<FileInfo> RolloutFiles(string home)
    {
        var sessions = Path.Join(home, "sessions");
        return Directory.Exists(sessions)
            ? new DirectoryInfo(sessions).EnumerateFiles("rollout-*.jsonl", SearchOption.AllDirectories)
            : [];
    }

    /// <summary>
    /// Every counted response must add up to the <c>total_tokens</c> its own line states. Reading it the Anthropic
    /// way — cache beside the input instead of inside it — would roughly double every session.
    /// </summary>
    [Fact]
    public void Every_counted_response_adds_up_to_what_its_line_says()
    {
        if (CodexHome is not { } home)
        {
            Assert.Skip("Set MCACC_REAL_CODEX to a Codex home to run this against real sessions.");
            return;
        }

        var checkedLines = 0;
        foreach (var file in RolloutFiles(home))
        {
            var reader = new CodexRolloutLineReader();
            foreach (var line in SharedLines(file))
            {
                if (!reader.TryRead(Encoding.UTF8.GetBytes(line), out var usage) || usage is null)
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                var stated = document.RootElement
                    .GetProperty("payload")
                    .GetProperty("usage")
                    .GetProperty("total_tokens")
                    .GetInt64();

                usage.Tokens.Total.ShouldBe(stated, $"{file.Name}: {usage.Model}");
                checkedLines++;
            }
        }

        checkedLines.ShouldBeGreaterThan(0, "no counted response found - is MCACC_REAL_CODEX a Codex home?");
    }

    /// <summary>Whatever the sessions hold, every counted response must name a model and a project.</summary>
    [Fact]
    public void Every_counted_response_is_attributed()
    {
        if (CodexHome is not { } home)
        {
            Assert.Skip("Set MCACC_REAL_CODEX to a Codex home to run this against real sessions.");
            return;
        }

        foreach (var file in RolloutFiles(home))
        {
            var reader = new CodexRolloutLineReader();
            foreach (var line in SharedLines(file))
            {
                if (reader.TryRead(Encoding.UTF8.GetBytes(line), out var usage) && usage is not null)
                {
                    usage.Model.ShouldNotBe("unknown", file.Name);
                    usage.Project.ShouldNotBeNullOrWhiteSpace(file.Name);
                    usage.Session.ShouldNotBeNullOrWhiteSpace(file.Name);
                }
            }
        }
    }
}
