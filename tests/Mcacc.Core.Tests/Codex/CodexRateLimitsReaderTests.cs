using System.Globalization;
using System.Text;
using Mcacc.Core.Codex;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Codex;

public sealed class CodexRateLimitsReaderTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Home => Path.Join(_temp.Path, ".codex");

    private static string Fixture(string name) => FixtureFiles.ReadAllText(name);

    /// <param name="name">Without the extension; the start time in the name is what orders the files.</param>
    private string WriteSession(string year, string month, string day, string name, string content)
    {
        var directory = Path.Join(Home, "sessions", year, month, day);
        Directory.CreateDirectory(directory);
        var path = Path.Join(directory, name + ".jsonl");
        File.WriteAllText(path, content);
        return path;
    }

    private CodexRateLimits? Read(int maxDays = 14, long maxBytesPerFile = 1 << 20) =>
        new CodexRateLimitsReader(maxDays, maxBytesPerFile).Read(Home);

    /// <summary>Within one file the last rate-limits line wins, and the truncated final line is skipped.</summary>
    [Fact]
    public void Takes_the_last_rate_limits_line_of_a_file()
    {
        WriteSession("2026", "10", "01", "rollout-2026-10-01T08-28-01-aaa", Fixture("codex-rollout.jsonl"));

        var limits = Read().ShouldNotBeNull();

        limits.FiveHour.ShouldNotBeNull().Utilization.ShouldBe(42.0);
        limits.At.ShouldBe(new DateTimeOffset(2026, 10, 1, 7, 15, 42, 250, TimeSpan.Zero));
    }

    [Fact]
    public void Nothing_at_all_yields_nothing()
    {
        Directory.CreateDirectory(Home);

        Read().ShouldBeNull();
    }

    [Fact]
    public void A_missing_sessions_folder_yields_nothing_without_throwing()
    {
        Read().ShouldBeNull();
    }

    /// <summary>The date partitions are numbers, so 2 must come after 10 — ordering by text would get this wrong.</summary>
    [Fact]
    public void The_newest_day_wins_across_years_months_and_days()
    {
        WriteSession("2025", "12", "31", "rollout-2025-12-31T23-00-00-aaa", Fixture("codex-rollout.jsonl"));
        WriteSession("2026", "02", "09", "rollout-2026-02-09T10-00-00-bbb", Fixture("codex-rollout.jsonl"));
        var newest = Line(used: 88.0, timestamp: "2026-10-02T09:00:00.000Z");
        WriteSession("2026", "10", "02", "rollout-2026-10-02T09-00-00-ccc", newest);

        Read().ShouldNotBeNull().FiveHour.ShouldNotBeNull().Utilization.ShouldBe(88.0);
    }

    /// <summary>Within a day the file name carries the start time, so no file is asked for its timestamp.</summary>
    [Fact]
    public void The_newest_file_of_a_day_wins_by_its_name()
    {
        WriteSession("2026", "10", "01", "rollout-2026-10-01T08-00-00-aaa", Line(11.0, "2026-10-01T08:00:00.000Z"));
        WriteSession("2026", "10", "01", "rollout-2026-10-01T21-00-00-bbb", Line(77.0, "2026-10-01T21:00:00.000Z"));
        WriteSession("2026", "10", "01", "rollout-2026-10-01T13-00-00-ccc", Line(44.0, "2026-10-01T13:00:00.000Z"));

        Read().ShouldNotBeNull().FiveHour.ShouldNotBeNull().Utilization.ShouldBe(77.0);
    }

    [Fact]
    public void A_file_without_limits_falls_through_to_the_previous_one()
    {
        WriteSession("2026", "10", "01", "rollout-2026-10-01T08-00-00-aaa", Line(33.0, "2026-10-01T08:00:00.000Z"));
        WriteSession("2026", "10", "01", "rollout-2026-10-01T22-00-00-bbb", Fixture("codex-rollout-no-limits.jsonl"));

        Read().ShouldNotBeNull().FiveHour.ShouldNotBeNull().Utilization.ShouldBe(33.0);
    }

    [Fact]
    public void A_day_without_limits_falls_through_to_the_previous_day()
    {
        WriteSession("2026", "09", "30", "rollout-2026-09-30T08-00-00-aaa", Line(21.0, "2026-09-30T08:00:00.000Z"));
        WriteSession("2026", "10", "01", "rollout-2026-10-01T08-00-00-bbb", Fixture("codex-rollout-no-limits.jsonl"));

        Read().ShouldNotBeNull().FiveHour.ShouldNotBeNull().Utilization.ShouldBe(21.0);
    }

    /// <summary>The walk has a budget, so a long-unused profile does not cost a full scan of its history.</summary>
    [Fact]
    public void The_day_budget_stops_the_walk()
    {
        WriteSession("2026", "09", "01", "rollout-2026-09-01T08-00-00-aaa", Line(50.0, "2026-09-01T08:00:00.000Z"));
        for (var day = 2; day <= 6; day++)
        {
            WriteSession("2026", "09", day.ToString("00"), $"rollout-2026-09-{day:00}T08-00-00-x", Fixture("codex-rollout-no-limits.jsonl"));
        }

        Read(maxDays: 3).ShouldBeNull();
        Read(maxDays: 14).ShouldNotBeNull();
    }

    /// <summary>
    /// The limits must be found even when the line sits beyond one read block, and the block boundary must not cut
    /// a line in two.
    /// </summary>
    [Fact]
    public void Finds_a_line_behind_a_hundred_kilobytes_of_other_lines()
    {
        var filler = new StringBuilder();
        for (var i = 0; i < 400; i++)
        {
            filler.Append("{\"timestamp\":\"2026-10-01T08:00:00.000Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"agent_message\",\"text\":\"")
                .Append('x', 250)
                .Append("\"}}\n");
        }

        WriteSession(
            "2026",
            "10",
            "01",
            "rollout-2026-10-01T08-00-00-aaa",
            Line(66.0, "2026-10-01T08:00:00.000Z") + filler);

        Read().ShouldNotBeNull().FiveHour.ShouldNotBeNull().Utilization.ShouldBe(66.0);
    }

    /// <summary>Beyond the byte budget the reader gives up on the file instead of reading all of it.</summary>
    [Fact]
    public void The_byte_budget_stops_the_search_inside_a_file()
    {
        var filler = new string('x', 300_000);
        WriteSession(
            "2026",
            "10",
            "01",
            "rollout-2026-10-01T08-00-00-aaa",
            Line(66.0, "2026-10-01T08:00:00.000Z")
            + "{\"type\":\"agent_message\",\"text\":\"" + filler + "\"}\n");

        Read(maxBytesPerFile: 4096).ShouldBeNull();
        Read().ShouldNotBeNull();
    }

    /// <summary>Codex holds the file of a running session open, so the reader must share it.</summary>
    [Fact]
    public void A_file_held_open_for_writing_is_still_read()
    {
        var path = WriteSession(
            "2026",
            "10",
            "01",
            "rollout-2026-10-01T08-00-00-aaa",
            Line(55.0, "2026-10-01T08:00:00.000Z"));
        using var holder = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        Read().ShouldNotBeNull().FiveHour.ShouldNotBeNull().Utilization.ShouldBe(55.0);
    }

    [Fact]
    public void A_directory_whose_name_is_not_a_number_is_ignored()
    {
        Directory.CreateDirectory(Path.Join(Home, "sessions", "backup", "10", "01"));
        WriteSession("2026", "10", "01", "rollout-2026-10-01T08-00-00-aaa", Line(12.0, "2026-10-01T08:00:00.000Z"));

        Read().ShouldNotBeNull().FiveHour.ShouldNotBeNull().Utilization.ShouldBe(12.0);
    }

    /// <summary>One session line carrying rate limits, shaped like the real ones.</summary>
    private static string Line(double used, string timestamp) =>
        "{\"timestamp\":\"" + timestamp + "\",\"ordinal\":5,\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\","
        + "\"rate_limits\":{\"primary\":{\"used_percent\":" + used.ToString(CultureInfo.InvariantCulture)
        + ",\"window_minutes\":300,\"resets_at\":1790854085},"
        + "\"secondary\":{\"used_percent\":1,\"window_minutes\":10080,\"resets_at\":1791440885},"
        + "\"plan_type\":\"plus\"}}}\n";
}
