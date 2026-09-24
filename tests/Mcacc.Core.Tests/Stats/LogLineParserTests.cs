using System.Text;
using Mcacc.Core.Stats;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public class LogLineParserTests
{
    private static LogUsage? Parse(string line)
    {
        LogLineParser.TryParse(Encoding.UTF8.GetBytes(line), out var usage).ShouldBeTrue();
        return usage;
    }

    [Fact]
    public void Reads_the_tokens_the_model_the_project_the_session_and_the_time()
    {
        var usage = Parse(LogLines.Assistant("msg_1", input: 10, output: 20, cacheRead: 300))!;

        usage.Tokens.ShouldBe(new TokenCounts(10, 20, 0, 0, 300));
        usage.Model.ShouldBe("claude-opus-4-5");
        usage.Project.ShouldBe(@"C:\Code\Mcacc");
        usage.Session.ShouldBe("s1");
        usage.Timestamp.ShouldBe(new DateTimeOffset(2026, 9, 20, 10, 15, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Splits_the_cache_writes_by_lifetime()
    {
        Parse(LogLines.Assistant("msg_1", cache5m: 100, cache1h: 50))!.Tokens
            .ShouldBe(new TokenCounts(10, 20, 100, 50, 0));
    }

    [Fact]
    public void Older_logs_count_the_whole_cache_write_as_5_minutes()
    {
        Parse(LogLines.Assistant("msg_1", legacyCacheWrite: 70))!.Tokens.ShouldBe(new TokenCounts(10, 20, 70, 0, 0));
    }

    [Theory]
    [InlineData("""{"type":"user","message":{"role":"user","content":"hallo"}}""")]
    [InlineData("not json and no marker")]
    [InlineData("")]
    public void A_line_without_usage_holds_none(string line)
    {
        Parse(line).ShouldBeNull();
    }

    [Fact]
    public void A_user_line_that_mentions_usage_holds_none()
    {
        Parse(LogLines.User("what is \"usage\"?")).ShouldBeNull();
    }

    [Fact]
    public void A_cut_off_line_with_usage_is_broken()
    {
        var line = Encoding.UTF8.GetBytes("""{"type":"assistant","message":{"usage":{"input_tokens":1""");

        LogLineParser.TryParse(line, out var usage).ShouldBeFalse();
        usage.ShouldBeNull();
    }

    [Fact]
    public void The_same_response_gets_the_same_id()
    {
        var first = Parse(LogLines.Assistant("msg_1", "req_1"))!;

        Parse(LogLines.Assistant("msg_1", "req_1", output: 99))!.Id.ShouldBe(first.Id);
        Parse(LogLines.Assistant("msg_1", "req_2"))!.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public void A_response_without_tokens_is_skipped()
    {
        Parse(LogLines.Assistant("msg_1", model: "<synthetic>", input: 0, output: 0)).ShouldBeNull();
    }

    [Fact]
    public void A_line_without_a_message_id_or_time_is_skipped()
    {
        Parse("""{"type":"assistant","timestamp":"2026-09-20T10:15:00Z","message":{"usage":{"input_tokens":5}}}""")
            .ShouldBeNull();
        Parse("""{"type":"assistant","message":{"id":"m","usage":{"input_tokens":5}}}""").ShouldBeNull();
    }

    [Fact]
    public void The_hash_is_stable()
    {
        LogLineParser.Hash("msg_1|req_1").ShouldBe(LogLineParser.Hash("msg_1|req_1"));
        LogLineParser.Hash("msg_1|req_1").ShouldNotBe(LogLineParser.Hash("msg_1|req_2"));
    }

    [Fact]
    public void Token_counts_add_up()
    {
        var sum = new TokenCounts(1, 2, 3, 4, 5) + new TokenCounts(10, 20, 30, 40, 50);

        sum.ShouldBe(new TokenCounts(11, 22, 33, 44, 55));
        sum.Total.ShouldBe(165);
        sum.CacheWrite.ShouldBe(77);
    }
}
