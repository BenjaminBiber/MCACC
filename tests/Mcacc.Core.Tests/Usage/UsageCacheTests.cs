using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public sealed class UsageCacheTests : IDisposable
{
    private static readonly DateTimeOffset FetchedAt = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly UsageSnapshot Snapshot = new(
        new UsageWindow(42.5, new DateTimeOffset(2026, 9, 23, 14, 30, 0, TimeSpan.Zero)),
        new UsageWindow(null, null),
        FetchedAt);

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private McaccPaths Paths => new(
        Path.Join(_temp.Path, ".mcacc"),
        OperatingSystem.IsWindows() ? PathStyle.Windows : PathStyle.Posix);

    private UsageCache Cache() => new(Paths);

    [Fact]
    public void An_absent_file_loads_as_empty()
    {
        Cache().Load().ShouldBeEmpty();
    }

    [Fact]
    public void Saved_snapshots_come_back()
    {
        Cache().Save(new Dictionary<string, UsageSnapshot> { ["privat"] = Snapshot });

        var loaded = Cache().Load();

        loaded.Count.ShouldBe(1);
        loaded["privat"].ShouldBe(Snapshot);
    }

    [Fact]
    public void A_corrupt_file_loads_as_empty()
    {
        Cache().Save(new Dictionary<string, UsageSnapshot> { ["privat"] = Snapshot });
        File.WriteAllText(Paths.UsageCacheFile, "{ not json");

        Cache().Load().ShouldBeEmpty();
    }

    [Fact]
    public void Saving_replaces_the_whole_file()
    {
        var cache = Cache();
        cache.Save(new Dictionary<string, UsageSnapshot> { ["privat"] = Snapshot, ["arbeit"] = Snapshot });

        cache.Save(new Dictionary<string, UsageSnapshot> { ["privat"] = Snapshot });

        cache.Load().Keys.ShouldBe(["privat"]);
    }

    [Fact]
    public void The_file_holds_no_token_like_field()
    {
        Cache().Save(new Dictionary<string, UsageSnapshot> { ["privat"] = Snapshot });

        File.ReadAllText(Paths.UsageCacheFile).ShouldNotContain("oken");
    }
}
