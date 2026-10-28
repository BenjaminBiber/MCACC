using Mcacc.Core.Codex;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Stats;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Codex;

public sealed class CodexStatsProviderTests : IDisposable
{
    private static readonly Account Codex =
        new("codex-privat", "Codex Privat", "#5E8C8A", IsDefault: false, ProviderKind.Codex);

    private readonly TempDirectory _temp = new();
    private readonly McaccPaths _paths;
    private readonly AccountPathResolver _resolver;

    public CodexStatsProviderTests()
    {
        var platform = PlatformInfo.Current() with { HomeDir = Path.Join(_temp.Path, "home") };
        _paths = new McaccPaths(Path.Join(_temp.Path, "mcacc"), platform.PathStyle);
        _resolver = new AccountPathResolver(_paths, platform);
    }

    public void Dispose() => _temp.Dispose();

    private string SessionsDir => Path.Join(_resolver.Resolve(Codex).EffectiveConfigDir, "sessions", "2026", "10", "01");

    private CodexStatsProvider Provider() => new(new StatsIndexStore(_paths), _resolver);

    private string Write(string name, params string[] lines)
    {
        Directory.CreateDirectory(SessionsDir);
        var path = Path.Join(SessionsDir, name);
        File.WriteAllText(path, string.Concat(lines.Select(line => line + "\n")));
        return path;
    }

    private static string Meta(string model, string cwd) =>
        "{\"timestamp\":\"2026-10-01T06:00:00.000Z\",\"type\":\"session_meta\",\"payload\":"
        + "{\"id\":\"s1\",\"cwd\":\"" + cwd.Replace(@"\", @"\\") + "\",\"model\":\"" + model + "\"}}";

    private static string Usage(string responseId, long input, long output, string minute = "10") =>
        "{\"timestamp\":\"2026-10-01T06:" + minute + ":00.000Z\",\"type\":\"token_usage_record\",\"payload\":"
        + "{\"thread_id\":\"th1\",\"response_id\":\"" + responseId + "\",\"usage\":{\"input_tokens\":" + input
        + ",\"cached_input_tokens\":0,\"cache_write_input_tokens\":0,\"output_tokens\":" + output
        + ",\"total_tokens\":" + (input + output) + "}}}";

    private async Task<IReadOnlyList<TokenBucket>> LoadAsync(CodexStatsProvider provider) =>
        await provider.LoadAsync(Codex, progress: null, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_session_file_is_counted_by_model_and_project()
    {
        Write("rollout-2026-10-01T06-00-00-aaa.jsonl", Meta("gpt-5-codex", @"C:\Repos\A"), Usage("r1", 100, 20));

        var buckets = await LoadAsync(Provider());

        var bucket = buckets.ShouldHaveSingleItem();
        bucket.Model.ShouldBe("gpt-5-codex");
        bucket.Project.ShouldBe(@"C:\Repos\A");
        bucket.Session.ShouldBe("th1");
        bucket.Tokens.Total.ShouldBe(120);
        bucket.Messages.ShouldBe(1);
    }

    [Fact]
    public async Task An_empty_profile_counts_nothing_without_throwing()
    {
        (await LoadAsync(Provider())).ShouldBeEmpty();
    }

    /// <summary>
    /// The reason the reader's state is written into the index: a second scan starts after the header and would
    /// otherwise file every new line under an unknown model.
    /// </summary>
    [Fact]
    public async Task A_resumed_scan_keeps_the_model_of_the_header_it_never_read()
    {
        var path = Write("rollout-2026-10-01T06-00-00-aaa.jsonl", Meta("gpt-5-codex", @"C:\Repos\A"), Usage("r1", 100, 20));
        var provider = Provider();
        await LoadAsync(provider);

        File.AppendAllText(path, Usage("r2", 50, 5, minute: "20") + "\n");
        var buckets = await LoadAsync(provider);

        buckets.ShouldAllBe(bucket => bucket.Model == "gpt-5-codex");
        buckets.ShouldAllBe(bucket => bucket.Project == @"C:\Repos\A");
        buckets.Sum(bucket => bucket.Tokens.Total).ShouldBe(175);
    }

    [Fact]
    public async Task The_same_response_in_two_files_counts_once()
    {
        Write("rollout-2026-10-01T06-00-00-aaa.jsonl", Meta("gpt-5-codex", @"C:\Repos\A"), Usage("r1", 100, 20));
        Write("rollout-2026-10-01T07-00-00-bbb.jsonl", Meta("gpt-5-codex", @"C:\Repos\A"), Usage("r1", 100, 20));

        var buckets = await LoadAsync(Provider());

        buckets.Sum(bucket => bucket.Tokens.Total).ShouldBe(120);
    }

    /// <summary>Codex writes no summary of its own, so there is nothing to add beside the scanned sessions.</summary>
    [Fact]
    public void There_is_no_precomputed_summary()
    {
        Provider().LoadSummary(Codex).ShouldBeEmpty();
    }

    [Fact]
    public async Task Forget_drops_the_index()
    {
        Write("rollout-2026-10-01T06-00-00-aaa.jsonl", Meta("gpt-5-codex", @"C:\Repos\A"), Usage("r1", 100, 20));
        var provider = Provider();
        await LoadAsync(provider);

        provider.Forget(Codex);

        File.Exists(_paths.StatsFile(Codex.Id)).ShouldBeFalse();
    }
}
