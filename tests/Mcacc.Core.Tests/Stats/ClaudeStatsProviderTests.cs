using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Stats;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public sealed class ClaudeStatsProviderTests : IDisposable
{
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PlatformInfo Platform => new(
        OperatingSystem.IsWindows() ? OsKind.Windows : OsKind.Linux,
        _temp.Path,
        Path.Join(_temp.Path, "Roaming"),
        Path.Join(_temp.Path, "Local"));

    private McaccPaths Paths => new(Path.Join(_temp.Path, ".mcacc"), Platform.PathStyle);

    private AccountPathResolver Resolver => new(Paths, Platform);

    private StatsIndexStore Store => new(Paths);

    private ClaudeStatsProvider Provider() => new(Store, Resolver);

    private string Log(params string[] lines)
    {
        var file = Path.Join(Resolver.Resolve(Privat).EffectiveConfigDir, "projects", "p1", "s1.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Concat(lines.Select(line => line + "\n")));
        return file;
    }

    [Fact]
    public async Task Loads_the_buckets_and_saves_the_index()
    {
        Log(LogLines.Assistant("msg_1"), LogLines.Assistant("msg_2"));

        var buckets = await Provider().LoadAsync(Privat, null, Ct);

        buckets.Sum(bucket => bucket.Messages).ShouldBe(2);
        Store.Load("privat").Files.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_index_outlives_a_deleted_log()
    {
        var file = Log(LogLines.Assistant("msg_1"));
        await Provider().LoadAsync(Privat, null, Ct);

        File.Delete(file);

        (await Provider().LoadAsync(Privat, null, Ct)).Sum(bucket => bucket.Messages).ShouldBe(1);
    }

    [Fact]
    public async Task Forget_deletes_the_index()
    {
        Log(LogLines.Assistant("msg_1"));
        var provider = Provider();
        await provider.LoadAsync(Privat, null, Ct);

        provider.Forget(Privat);

        File.Exists(Paths.StatsFile("privat")).ShouldBeFalse();
    }

    [Fact]
    public async Task Parallel_loads_count_each_response_once()
    {
        Log(LogLines.Assistant("msg_1"), LogLines.Assistant("msg_2"));
        var provider = Provider();

        var results = await Task.WhenAll(
            provider.LoadAsync(Privat, null, Ct),
            provider.LoadAsync(Privat, null, Ct));

        results.ShouldAllBe(buckets => buckets.Sum(bucket => bucket.Messages) == 2);
        Store.Load("privat").Files.ShouldHaveSingleItem().Ids.Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_account_without_logs_has_no_buckets()
    {
        (await Provider().LoadAsync(Privat, null, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Forget_racing_the_post_scan_write_never_leaves_the_index_behind()
    {
        Log(LogLines.Assistant("msg_1"));
        var provider = Provider();

        // The window between the post-scan check and the write is a handful of instructions; repeating the race
        // hammers it until the scheduler lands Forget's delete inside it (it does, reliably, within a few tries).
        for (var i = 0; i < 200; i++)
        {
            Task? forget = null;
            await provider.LoadAsync(Privat, new RacingProgress(() => forget = Task.Run(() => provider.Forget(Privat))), Ct);
            if (forget is not null)
            {
                await forget;
            }

            File.Exists(Paths.StatsFile("privat")).ShouldBeFalse();
        }
    }

    /// <summary>Fires an action off the scan thread right after its last report, i.e. just before the post-scan write.</summary>
    private sealed class RacingProgress(Action onFinalReport) : IProgress<double>
    {
        public void Report(double value)
        {
            if (value >= 1)
            {
                onFinalReport();
            }
        }
    }
}
