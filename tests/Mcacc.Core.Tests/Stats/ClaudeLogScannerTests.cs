using System.Text;
using Mcacc.Core.Stats;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public sealed class ClaudeLogScannerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Claude => Path.Join(_temp.Path, ".claude");

    private string Log(string relative, params string[] lines)
    {
        var file = Path.Join(Claude, "projects", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Concat(lines.Select(line => line + "\n")));
        return file;
    }

    private LogScanResult Scan(StatsIndex? index = null, IProgress<double>? progress = null) =>
        ClaudeLogScanner.Update(Claude, index ?? StatsIndex.Empty, progress, Ct);

    private static IEnumerable<TokenBucket> Buckets(StatsIndex index) => index.Files.SelectMany(file => file.Buckets);

    private static TokenCounts Sum(StatsIndex index) =>
        Buckets(index).Aggregate(TokenCounts.Zero, (sum, bucket) => sum + bucket.Tokens);

    private static int Messages(StatsIndex index) => Buckets(index).Sum(bucket => bucket.Messages);

    [Fact]
    public void A_response_logged_once_per_content_block_counts_once()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"), LogLines.Assistant("msg_1"), LogLines.Assistant("msg_2"));

        var index = Scan().Index;

        Messages(index).ShouldBe(2);
        Sum(index).Input.ShouldBe(20);
    }

    [Fact]
    public void A_response_copied_into_another_session_counts_once()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        Log("p1/s2.jsonl", LogLines.Assistant("msg_1"));

        Messages(Scan().Index).ShouldBe(1);
    }

    [Fact]
    public void The_subagent_logs_count()
    {
        Log(Path.Join("p1", "s1", "subagents", "agent-a1.jsonl"), LogLines.Assistant("msg_1"));

        var index = Scan().Index;

        Messages(index).ShouldBe(1);
        index.Files.ShouldHaveSingleItem().Path.ShouldBe(Path.Join("p1", "s1", "subagents", "agent-a1.jsonl"));
    }

    [Fact]
    public void Broken_lines_are_skipped_and_counted()
    {
        Log(
            "p1/s1.jsonl",
            LogLines.Assistant("msg_1"),
            """{"type":"assistant","message":{"usage":{""",
            LogLines.User());

        var file = Scan().Index.Files.ShouldHaveSingleItem();

        file.Buckets.Sum(bucket => bucket.Messages).ShouldBe(1);
        file.BrokenLines.ShouldBe(1);
    }

    [Fact]
    public void A_line_still_being_written_is_read_on_the_next_scan()
    {
        var first = LogLines.Assistant("msg_1");
        var second = LogLines.Assistant("msg_2");
        var file = Log("p1/s1.jsonl", first);
        File.AppendAllText(file, second[..20]);

        var before = Scan().Index;
        before.Files.ShouldHaveSingleItem().Offset.ShouldBe(Encoding.UTF8.GetByteCount(first) + 1);
        Messages(before).ShouldBe(1);

        File.AppendAllText(file, second[20..] + "\n");

        Messages(Scan(before).Index).ShouldBe(2);
    }

    [Fact]
    public void A_grown_file_is_read_on_from_the_last_offset()
    {
        var file = Log("p1/s1.jsonl", LogLines.Assistant("msg_1", output: 20));
        var before = Scan().Index;

        // Same length, other digits: had the scanner started over, it would count 29 instead of 20.
        File.WriteAllText(
            file,
            LogLines.Assistant("msg_1", output: 29) + "\n" + LogLines.Assistant("msg_2", output: 5) + "\n");

        Sum(Scan(before).Index).Output.ShouldBe(25);
    }

    [Fact]
    public void Unchanged_logs_leave_the_index_as_it_is()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        var before = Scan().Index;

        var result = Scan(before);

        result.Changed.ShouldBeFalse();
        result.Index.ShouldBeSameAs(before);
    }

    [Fact]
    public void A_shrunk_file_is_read_from_the_start()
    {
        var file = Log("p1/s1.jsonl", LogLines.Assistant("msg_1"), LogLines.Assistant("msg_2"));
        var before = Scan().Index;

        File.WriteAllText(file, LogLines.Assistant("msg_3", input: 7) + "\n");
        var index = Scan(before).Index;

        Messages(index).ShouldBe(1);
        Sum(index).Input.ShouldBe(7);
    }

    [Fact]
    public void A_deleted_log_keeps_its_counts()
    {
        var file = Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        var before = Scan().Index;

        File.Delete(file);
        var result = Scan(before);

        result.Changed.ShouldBeFalse();
        Messages(result.Index).ShouldBe(1);
    }

    [Fact]
    public void Without_a_projects_folder_nothing_is_found()
    {
        var result = Scan();

        result.Changed.ShouldBeFalse();
        result.Index.Files.ShouldBeEmpty();
    }

    [Fact]
    public void Buckets_are_cut_per_utc_hour_model_project_and_session()
    {
        Log(
            "p1/s1.jsonl",
            LogLines.Assistant("msg_1", timestamp: "2026-09-20T10:15:00.000Z"),
            LogLines.Assistant("msg_2", timestamp: "2026-09-20T10:45:00.000Z"),
            LogLines.Assistant("msg_3", timestamp: "2026-09-20T11:05:00.000Z"),
            LogLines.Assistant("msg_4", timestamp: "2026-09-20T10:20:00.000Z", model: "claude-sonnet-4-5"),
            LogLines.Assistant("msg_5", timestamp: "2026-09-20T10:25:00.000Z", session: "s2"));

        var buckets = Scan().Index.Files.ShouldHaveSingleItem().Buckets;

        buckets.Count.ShouldBe(4);
        buckets[0].ShouldBe(new TokenBucket(
            new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
            "claude-opus-4-5",
            @"C:\Code\Mcacc",
            "s1",
            new TokenCounts(20, 40, 0, 0, 0),
            2));
    }

    [Fact]
    public void Progress_ends_at_one()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        var progress = new RecordingProgress();

        Scan(progress: progress);

        progress.Values.ShouldNotBeEmpty();
        progress.Values.ShouldAllBe(value => value >= 0 && value <= 1);
        progress.Values[^1].ShouldBe(1);
    }

    [Fact]
    public void An_index_of_another_version_is_rebuilt()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        var stale = new StatsIndex(0, [new IndexedLogFile("ghost.jsonl", 1, DateTime.UnixEpoch, 1, [], [42], 0)]);

        var result = Scan(stale);

        result.Changed.ShouldBeTrue();
        result.Index.Version.ShouldBe(StatsIndex.CurrentVersion);
        result.Index.Files.ShouldHaveSingleItem().Path.ShouldBe(Path.Join("p1", "s1.jsonl"));
    }

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }
}
