using System.Text.Json;
using Mcacc.Core.Config;
using Mcacc.Core.Logging;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public sealed class UsageHistoryStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(Now, TimeZoneInfo.Utc);

    public void Dispose() => _temp.Dispose();

    private McaccPaths Paths => new(
        Path.Join(_temp.Path, ".mcacc"),
        OperatingSystem.IsWindows() ? PathStyle.Windows : PathStyle.Posix);

    private string File => Paths.UsageHistoryFile("privat");

    private UsageHistoryStore Store() => new(Paths, _time);

    private static UsageHistoryPoint Point(DateTimeOffset at) => new(at, 10, null, 5, null);

    private static string Line(UsageHistoryPoint point) =>
        JsonSerializer.Serialize(point, McaccCompactJsonContext.Default.UsageHistoryPoint) + "\n";

    private void WriteLines(params UsageHistoryPoint[] points)
    {
        Directory.CreateDirectory(Paths.UsageHistoryDir);
        System.IO.File.WriteAllText(File, string.Concat(points.Select(Line)));
    }

    [Fact]
    public void Appended_points_read_back_in_time_order()
    {
        var store = Store();

        store.Append("privat", Point(Now));
        store.Append("privat", Point(Now - TimeSpan.FromHours(1)));

        store.Read("privat", DateTimeOffset.MinValue).ShouldBe([Point(Now - TimeSpan.FromHours(1)), Point(Now)]);
    }

    [Fact]
    public void Read_starts_at_since()
    {
        var store = Store();
        store.Append("privat", Point(Now - TimeSpan.FromDays(2)));
        store.Append("privat", Point(Now));

        store.Read("privat", Now - TimeSpan.FromDays(1)).ShouldBe([Point(Now)]);
    }

    [Fact]
    public void Each_point_is_one_compact_line()
    {
        var store = Store();
        store.Append("privat", Point(Now));
        store.Append("privat", Point(Now));

        var lines = System.IO.File.ReadAllText(File).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(2);
        lines.ShouldAllBe(line => line.StartsWith('{') && !line.Contains("  "));
    }

    [Fact]
    public void A_broken_line_is_skipped()
    {
        WriteLines(Point(Now - TimeSpan.FromHours(1)));
        System.IO.File.AppendAllText(File, "{\"at\": \"2026-09\n");

        Store().Read("privat", DateTimeOffset.MinValue).ShouldBe([Point(Now - TimeSpan.FromHours(1))]);
    }

    [Fact]
    public void The_first_append_of_a_day_drops_points_older_than_90_days()
    {
        WriteLines(Point(Now - TimeSpan.FromDays(91)), Point(Now - TimeSpan.FromDays(89)));

        Store().Append("privat", Point(Now));

        Store().Read("privat", DateTimeOffset.MinValue).ShouldBe([Point(Now - TimeSpan.FromDays(89)), Point(Now)]);
    }

    [Fact]
    public void Pruning_runs_at_most_once_a_day()
    {
        var store = Store();
        store.Append("privat", Point(Now));
        System.IO.File.AppendAllText(File, Line(Point(Now - TimeSpan.FromDays(100))));

        store.Append("privat", Point(Now + TimeSpan.FromMinutes(5)));
        store.Read("privat", DateTimeOffset.MinValue).Count.ShouldBe(3);

        _time.Advance(TimeSpan.FromDays(1));
        store.Append("privat", Point(Now + TimeSpan.FromDays(1)));
        store.Read("privat", DateTimeOffset.MinValue).Count.ShouldBe(3);
        store.Read("privat", DateTimeOffset.MinValue).ShouldAllBe(point => point.At >= Now);
    }

    [Fact]
    public void Delete_removes_the_history()
    {
        var store = Store();
        store.Append("privat", Point(Now));

        store.Delete("privat");

        System.IO.File.Exists(File).ShouldBeFalse();
        store.Read("privat", DateTimeOffset.MinValue).ShouldBeEmpty();
    }

    [Fact]
    public void Without_a_file_the_history_is_empty()
    {
        Store().Read("privat", DateTimeOffset.MinValue).ShouldBeEmpty();
        Store().Delete("privat");
    }

    [Fact]
    public void A_point_takes_both_windows_of_a_snapshot()
    {
        var resets = Now + TimeSpan.FromHours(2);
        var snapshot = new UsageSnapshot(new UsageWindow(42, resets), new UsageWindow(null, null), Now);

        UsageHistoryPoint.From(snapshot).ShouldBe(new UsageHistoryPoint(Now, 42, resets, null, null));
    }

    [Fact]
    public void A_failing_append_is_logged()
    {
        var log = new RecordingLog();
        var store = new UsageHistoryStore(Paths, _time, log);
        store.Append("privat", Point(Now)); // succeeds, so pruning already ran for today and will not read again

        using (new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            store.Append("privat", Point(Now));
        }

        var warning = log.Warnings.ShouldHaveSingleItem();
        warning.Exception.ShouldNotBeNull();
    }

    private sealed class RecordingLog : ILog
    {
        public List<(string Message, Exception? Exception)> Warnings { get; } = [];

        public void Info(string message)
        {
        }

        public void Warn(string message, Exception? exception = null) => Warnings.Add((message, exception));

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
