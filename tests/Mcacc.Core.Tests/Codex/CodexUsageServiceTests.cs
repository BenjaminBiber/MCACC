using Mcacc.Core.Abstractions;
using Mcacc.Core.Codex;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Codex;

public sealed class CodexUsageServiceTests : IDisposable
{
    private static readonly Account Codex =
        new("codex-privat", "Codex Privat", "#5E8C8A", IsDefault: false, ProviderKind.Codex);

    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _temp = new();
    private readonly McaccPaths _paths;
    private readonly AccountPathResolver _resolver;
    private readonly FakeCodexUsageHttp _http = new();
    private readonly FakeTimeProvider _time = new(Now, TimeZoneInfo.Utc);

    public CodexUsageServiceTests()
    {
        var platform = PlatformInfo.Current() with { HomeDir = Path.Join(_temp.Path, "home") };
        _paths = new McaccPaths(Path.Join(_temp.Path, "mcacc"), platform.PathStyle);
        _resolver = new AccountPathResolver(_paths, platform);
    }

    public void Dispose() => _temp.Dispose();

    private string CodexHome => _resolver.Resolve(Codex).EffectiveConfigDir;

    private CodexUsageService Service() => new(
        _http,
        new CodexRateLimitsReader(),
        new UsageCache(_paths),
        new ProfileCache(_paths),
        new UsageHistoryStore(_paths, _time),
        _resolver,
        _time);

    private void WriteAuth(string fixture = "codex-auth.json")
    {
        Directory.CreateDirectory(CodexHome);
        File.WriteAllText(Path.Join(CodexHome, "auth.json"), FixtureFiles.ReadAllText(fixture));
    }

    private void WriteSession(string timestamp, double used)
    {
        var directory = Path.Join(CodexHome, "sessions", "2026", "10", "01");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Join(directory, "rollout-2026-10-01T08-00-00-aaa.jsonl"),
            "{\"timestamp\":\"" + timestamp + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\","
            + "\"rate_limits\":{\"primary\":{\"used_percent\":" + used + ",\"window_minutes\":300,\"resets_at\":1790854085},"
            + "\"secondary\":{\"used_percent\":1,\"window_minutes\":10080,\"resets_at\":1791440885}}}}\n");
    }

    private async Task<AccountUsage> RefreshAsync(CodexUsageService service) =>
        await service.RefreshAsync(Codex, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_reachable_endpoint_gives_a_live_snapshot()
    {
        WriteAuth();
        _http.Reply = new UsageHttpResponse(200, FixtureFiles.ReadAllText("codex-usage-live.json"));

        var usage = await RefreshAsync(Service());

        usage.State.ShouldBe(UsageState.Ok);
        var snapshot = usage.Snapshot.ShouldNotBeNull();
        snapshot.Source.ShouldBe(UsageSource.Live);
        snapshot.FetchedAt.ShouldBe(Now);
        snapshot.FiveHour.ShouldNotBeNull().Utilization.ShouldBe(29.5);
    }

    /// <summary>
    /// The whole point of the fallback: no endpoint, but the numbers Codex wrote itself — carrying their own age,
    /// so the tile can say how old they are instead of showing them as current.
    /// </summary>
    [Fact]
    public async Task An_unreachable_endpoint_falls_back_to_the_session_files_as_stale()
    {
        WriteAuth();
        WriteSession("2026-10-01T07:15:42.250Z", used: 42.0);
        _http.Reply = new UsageHttpResponse(503, "");

        var usage = await RefreshAsync(Service());

        usage.State.ShouldBe(UsageState.Stale);
        var snapshot = usage.Snapshot.ShouldNotBeNull();
        snapshot.Source.ShouldBe(UsageSource.LocalFile);
        snapshot.FiveHour.ShouldNotBeNull().Utilization.ShouldBe(42.0);
        snapshot.FetchedAt.ShouldBe(new DateTimeOffset(2026, 10, 1, 7, 15, 42, 250, TimeSpan.Zero));
        snapshot.FetchedAt.ShouldNotBe(Now);
    }

    [Fact]
    public async Task A_thrown_request_also_falls_back_to_the_session_files()
    {
        WriteAuth();
        WriteSession("2026-10-01T07:15:42.250Z", used: 42.0);
        _http.Throw = new HttpRequestException("no network");

        (await RefreshAsync(Service())).State.ShouldBe(UsageState.Stale);
    }

    [Fact]
    public async Task Without_an_endpoint_and_without_session_files_the_failure_stands()
    {
        WriteAuth();
        _http.Reply = new UsageHttpResponse(503, "");

        var usage = await RefreshAsync(Service());

        usage.State.ShouldBe(UsageState.Error);
        usage.Snapshot.ShouldBeNull();
    }

    [Fact]
    public async Task An_unauthorized_endpoint_without_session_files_says_so()
    {
        WriteAuth();
        _http.Reply = new UsageHttpResponse(401, "");

        (await RefreshAsync(Service())).State.ShouldBe(UsageState.Unauthorized);
    }

    [Fact]
    public async Task A_profile_without_auth_json_is_not_logged_in_and_asks_nothing()
    {
        Directory.CreateDirectory(CodexHome);

        var usage = await RefreshAsync(Service());

        usage.State.ShouldBe(UsageState.NotLoggedIn);
        _http.Calls.ShouldBe(0);
    }

    /// <summary>An API key is no OAuth token, so the endpoint is not even asked.</summary>
    [Fact]
    public async Task An_api_key_login_asks_nothing()
    {
        WriteAuth("codex-auth-apikey.json");

        (await RefreshAsync(Service())).State.ShouldBe(UsageState.NotLoggedIn);
        _http.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task The_endpoint_gets_the_account_id_in_a_header_argument()
    {
        WriteAuth();
        _http.Reply = new UsageHttpResponse(200, FixtureFiles.ReadAllText("codex-usage-live.json"));

        await RefreshAsync(Service());

        _http.LastAccountId.ShouldBe("cff32eb3-0000-0000-0000-000000000000");
    }

    /// <summary>
    /// The five-minute poller would otherwise append the same session line to the history for as long as Codex is
    /// not used, filling the chart with a flat line of duplicates.
    /// </summary>
    [Fact]
    public async Task The_same_session_line_reaches_the_history_only_once()
    {
        WriteAuth();
        WriteSession("2026-10-01T07:15:42.250Z", used: 42.0);
        _http.Reply = new UsageHttpResponse(503, "");
        var service = Service();

        await RefreshAsync(service);
        service.Invalidate(Codex);
        await RefreshAsync(service);
        service.Invalidate(Codex);
        await RefreshAsync(service);

        service.GetHistory(Codex, DateTimeOffset.UnixEpoch).Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_newer_session_line_does_reach_the_history()
    {
        WriteAuth();
        WriteSession("2026-10-01T07:15:42.250Z", used: 42.0);
        _http.Reply = new UsageHttpResponse(503, "");
        var service = Service();
        await RefreshAsync(service);

        WriteSession("2026-10-01T09:30:00.000Z", used: 55.0);
        service.Invalidate(Codex);
        await RefreshAsync(service);

        var history = service.GetHistory(Codex, DateTimeOffset.UnixEpoch);
        history.Count.ShouldBe(2);
        history[^1].FiveHour.ShouldBe(55.0);
    }

    [Fact]
    public async Task Asks_the_endpoint_at_most_once_a_minute()
    {
        WriteAuth();
        _http.Reply = new UsageHttpResponse(200, FixtureFiles.ReadAllText("codex-usage-live.json"));
        var service = Service();

        await RefreshAsync(service);
        await RefreshAsync(service);

        _http.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task An_expired_token_is_not_sent_to_the_endpoint()
    {
        WriteAuth();
        WriteSession("2026-10-01T07:15:42.250Z", used: 42.0);
        _time.Advance(TimeSpan.FromDays(10_000));

        var usage = await RefreshAsync(Service());

        _http.Calls.ShouldBe(0);
        usage.State.ShouldBe(UsageState.Stale);
    }

    [Fact]
    public async Task A_live_refresh_remembers_the_owner_as_the_profile()
    {
        WriteAuth();
        _http.Reply = new UsageHttpResponse(200, FixtureFiles.ReadAllText("codex-usage-live.json"));
        var service = Service();

        await RefreshAsync(service);

        var profile = service.GetProfile(Codex).ShouldNotBeNull();
        profile.Email.ShouldBe("ben@example.com");
        profile.AccountUuid.ShouldBe("cff32eb3-0000-0000-0000-000000000000");
        profile.RateLimitTier.ShouldBe("plus");
        profile.OrganizationName.ShouldBe("Hauptorg");
    }

    /// <summary>
    /// Both values come from the same machine, but a refresh that rewrote only one of them could make them
    /// disagree — and then the numbers belong to someone else.
    /// </summary>
    [Fact]
    public async Task A_reply_naming_another_account_shows_no_numbers_at_all()
    {
        WriteAuth();
        _http.Reply = new UsageHttpResponse(
            200,
            FixtureFiles.ReadAllText("codex-usage-live.json")
                .Replace("cff32eb3-0000-0000-0000-000000000000", "ffffffff-0000-0000-0000-000000000000")
                .Replace("ben@example.com", "someone@else.test"));

        var usage = await RefreshAsync(Service());

        usage.State.ShouldBe(UsageState.WrongAccount);
        usage.Snapshot.ShouldBeNull();
        usage.TokenEmail.ShouldBe("someone@else.test");
    }

    [Fact]
    public async Task Forget_drops_the_limits_the_profile_and_the_history()
    {
        WriteAuth();
        _http.Reply = new UsageHttpResponse(200, FixtureFiles.ReadAllText("codex-usage-live.json"));
        var service = Service();
        await RefreshAsync(service);

        service.Forget(Codex);

        service.GetCached(Codex).State.ShouldBe(UsageState.NotLoggedIn);
        service.GetProfile(Codex).ShouldBeNull();
        service.GetHistory(Codex, DateTimeOffset.UnixEpoch).ShouldBeEmpty();
    }

    /// <summary>A cached local value must keep saying it is local after a restart.</summary>
    [Fact]
    public async Task A_cached_local_value_is_still_reported_as_stale()
    {
        WriteAuth();
        WriteSession("2026-10-01T07:15:42.250Z", used: 42.0);
        _http.Reply = new UsageHttpResponse(503, "");
        await RefreshAsync(Service());

        var fresh = Service();

        fresh.GetCached(Codex).State.ShouldBe(UsageState.Stale);
    }

    private sealed class FakeCodexUsageHttp : ICodexUsageHttp
    {
        public UsageHttpResponse Reply { get; set; } = new(503, "");

        public Exception? Throw { get; set; }

        public int Calls { get; private set; }

        public string? LastAccountId { get; private set; }

        public Task<UsageHttpResponse> GetUsageAsync(string accessToken, string? accountId, CancellationToken ct)
        {
            Calls++;
            LastAccountId = accountId;
            return Throw is { } error ? Task.FromException<UsageHttpResponse>(error) : Task.FromResult(Reply);
        }
    }
}
