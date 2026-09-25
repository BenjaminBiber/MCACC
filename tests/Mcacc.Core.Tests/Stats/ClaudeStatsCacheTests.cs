using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public sealed class ClaudeStatsCacheTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("mcacc-stats-cache-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Write(string json) => File.WriteAllText(Path.Join(_dir, ClaudeStatsCache.FileName), json);

    [Fact]
    public void The_daily_tokens_are_summed_over_the_models_oldest_first()
    {
        Write("""
            {
              "version": 4,
              "dailyActivity": [{ "date": "2026-07-27", "messageCount": 884 }],
              "dailyModelTokens": [
                { "date": "2026-08-03", "tokensByModel": { "claude-opus-4-8": 100 } },
                { "date": "2026-07-27", "tokensByModel": { "claude-opus-4-8": 1700, "claude-haiku-4-5": 10 } }
              ]
            }
            """);

        ClaudeStatsCache.Read(_dir).ShouldBe(
        [
            new ClaudeDailyTokens(new DateOnly(2026, 7, 27), 1710),
            new ClaudeDailyTokens(new DateOnly(2026, 8, 3), 100),
        ]);
    }

    [Fact]
    public void Broken_entries_and_days_without_tokens_are_skipped()
    {
        Write("""
            {
              "dailyModelTokens": [
                { "date": "27.07.2026", "tokensByModel": { "a": 1 } },
                { "date": "2026-07-28" },
                { "date": "2026-07-29", "tokensByModel": { "a": "viel" } },
                { "date": "2026-07-30", "tokensByModel": { "a": 0 } },
                42,
                { "date": "2026-07-31", "tokensByModel": { "a": 5 } }
              ]
            }
            """);

        ClaudeStatsCache.Read(_dir).ShouldHaveSingleItem().ShouldBe(new ClaudeDailyTokens(new DateOnly(2026, 7, 31), 5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ nicht json")]
    [InlineData("[]")]
    [InlineData("""{ "dailyModelTokens": {} }""")]
    public void A_missing_or_unreadable_file_reads_as_empty(string? json)
    {
        if (json is not null)
        {
            Write(json);
        }

        ClaudeStatsCache.Read(_dir).ShouldBeEmpty();
    }
}
