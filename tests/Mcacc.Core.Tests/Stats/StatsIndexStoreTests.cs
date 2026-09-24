using System.Text.Json;
using Mcacc.Core.Config;
using Mcacc.Core.Paths;
using Mcacc.Core.Stats;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public sealed class StatsIndexStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private McaccPaths Paths => new(
        Path.Join(_temp.Path, ".mcacc"),
        OperatingSystem.IsWindows() ? PathStyle.Windows : PathStyle.Posix);

    private static readonly StatsIndex Sample = new(
        StatsIndex.CurrentVersion,
        [
            new IndexedLogFile(
                Path.Join("p1", "s1.jsonl"),
                1234,
                new DateTime(2026, 9, 20, 10, 15, 0, DateTimeKind.Utc),
                1200,
                [
                    new TokenBucket(
                        new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
                        "claude-opus-4-5",
                        @"C:\Code\Mcacc",
                        "s1",
                        new TokenCounts(1, 2, 3, 4, 5),
                        6),
                ],
                [42, -7],
                1),
        ]);

    private static string Json(StatsIndex index) =>
        JsonSerializer.Serialize(index, McaccCompactJsonContext.Default.StatsIndex);

    [Fact]
    public void A_saved_index_loads_back()
    {
        var store = new StatsIndexStore(Paths);

        store.Save("privat", Sample);

        Json(store.Load("privat")).ShouldBe(Json(Sample));
    }

    [Fact]
    public void Without_a_file_the_index_is_empty()
    {
        new StatsIndexStore(Paths).Load("privat").ShouldBeSameAs(StatsIndex.Empty);
    }

    [Fact]
    public void An_index_of_another_version_loads_as_empty()
    {
        var store = new StatsIndexStore(Paths);
        store.Save("privat", Sample with { Version = 0 });

        store.Load("privat").ShouldBeSameAs(StatsIndex.Empty);
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        var store = new StatsIndexStore(Paths);
        store.Save("privat", Sample);

        store.Delete("privat");
        store.Delete("privat");

        File.Exists(Paths.StatsFile("privat")).ShouldBeFalse();
    }
}
