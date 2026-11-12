using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ClaudeCode;

public sealed class ClaudeLimitsFileTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 30, 0, TimeSpan.Zero);

    private const string Full = """
        {
          "session_id": "abc",
          "model": { "id": "claude-opus-5-5" },
          "rate_limits": {
            "five_hour": { "used_percentage": 23.5, "resets_at": 1794486600 },
            "seven_day": { "used_percentage": 41, "resets_at": 1794990600 }
          }
        }
        """;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Dir => Path.Join(_temp.Path, "claude");

    [Fact]
    public void A_status_line_input_round_trips_into_a_local_snapshot()
    {
        ClaudeLimitsFile.Record(Dir, Full, Now).ShouldBeTrue();

        ClaudeLimitsFile.Read(Dir).ShouldBe(new UsageSnapshot(
            new UsageWindow(23.5, DateTimeOffset.FromUnixTimeSeconds(1794486600)),
            new UsageWindow(41, DateTimeOffset.FromUnixTimeSeconds(1794990600)),
            Now,
            Source: UsageSource.LocalFile));
    }

    [Fact]
    public void A_window_past_its_reset_is_missing_on_its_own()
    {
        const string json = """{ "rate_limits": { "seven_day": { "used_percentage": 12, "resets_at": 1794990600 } } }""";

        ClaudeLimitsFile.Record(Dir, json, Now).ShouldBeTrue();

        var snapshot = ClaudeLimitsFile.Read(Dir).ShouldNotBeNull();
        snapshot.FiveHour.ShouldBeNull();
        snapshot.SevenDay.ShouldBe(new UsageWindow(12, DateTimeOffset.FromUnixTimeSeconds(1794990600)));
    }

    [Theory]
    [InlineData("""{ "session_id": "abc" }""")]
    [InlineData("""{ "rate_limits": {} }""")]
    [InlineData("""{ "rate_limits": { "five_hour": { "resets_at": 1 } } }""")]
    [InlineData("not json")]
    [InlineData("")]
    public void An_input_without_limits_leaves_an_older_file_standing(string json)
    {
        ClaudeLimitsFile.Record(Dir, Full, Now);

        ClaudeLimitsFile.Record(Dir, json, Now.AddHours(1)).ShouldBeFalse();

        ClaudeLimitsFile.Read(Dir).ShouldNotBeNull().FetchedAt.ShouldBe(Now);
    }

    [Fact]
    public void No_file_reads_as_nothing() => ClaudeLimitsFile.Read(Dir).ShouldBeNull();

    [Fact]
    public void A_broken_file_reads_as_nothing()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Join(Dir, ClaudeLimitsFile.FileName), "{ \"recorded_at\": ");

        ClaudeLimitsFile.Read(Dir).ShouldBeNull();
    }

    [Fact]
    public void Recording_leaves_no_temp_file_behind()
    {
        ClaudeLimitsFile.Record(Dir, Full, Now);

        Directory.GetFiles(Dir).Select(Path.GetFileName).ShouldBe([ClaudeLimitsFile.FileName]);
    }
}
