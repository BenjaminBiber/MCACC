using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public sealed class UsageServiceTests : IDisposable
{
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);
    private static readonly Account Arbeit = new("arbeit", "Arbeit", "#788C5D", IsDefault: false);

    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private const string Token = "sk-ant-oat01-EXAMPLE-NOT-A-REAL-TOKEN";

    private static readonly UsageSnapshot Old = new(
        new UsageWindow(11, new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero)),
        null,
        Now - TimeSpan.FromHours(3));

    private readonly TempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(Now, TimeZoneInfo.Utc);
    private readonly FakeCredentialSource _credentials = new();
    private readonly FakeUsageHttp _http = new();

    public UsageServiceTests()
    {
        _credentials.Result = new CredentialReadResult(
            CredentialReadStatus.Found,
            new OAuthCredentials(Token, Now + TimeSpan.FromHours(1)));
        _http.Response = new UsageHttpResponse(200, FixtureFiles.ReadAllText("usage-ok.json"));
    }

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PlatformInfo Platform => new(
        OperatingSystem.IsWindows() ? OsKind.Windows : OsKind.Linux,
        _temp.Path,
        Path.Join(_temp.Path, "Roaming"),
        Path.Join(_temp.Path, "Local"));

    private McaccPaths Paths => new(Path.Join(_temp.Path, ".mcacc"), Platform.PathStyle);

    private UsageCache Cache => new(Paths);

    private UsageService Service() =>
        new(_credentials, _http, Cache, new AccountPathResolver(Paths, Platform), _time);

    private void SeedCache(params string[] ids) =>
        Cache.Save(ids.ToDictionary(id => id, _ => Old));

    private static UsageSnapshot Fresh(DateTimeOffset fetchedAt) => new(
        new UsageWindow(42.5, new DateTimeOffset(2026, 9, 23, 14, 30, 0, TimeSpan.Zero)),
        new UsageWindow(7, new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.FromHours(2))),
        fetchedAt);

    [Fact]
    public void Cached_returns_the_stored_snapshot()
    {
        SeedCache("privat");

        Service().GetCached(Privat).ShouldBe(new AccountUsage(UsageState.Ok, Old));
    }

    [Fact]
    public void Cached_without_an_entry_reports_no_login()
    {
        SeedCache("arbeit");

        Service().GetCached(Privat).ShouldBe(new AccountUsage(UsageState.NotLoggedIn, null));
    }

    [Fact]
    public async Task Refresh_stores_the_fresh_snapshot_in_the_cache_file()
    {
        var usage = await Service().RefreshAsync(Privat, Ct);

        usage.ShouldBe(new AccountUsage(UsageState.Ok, Fresh(Now)));
        _http.Requests.ShouldBe([Token]);
        Cache.Load()["privat"].ShouldBe(Fresh(Now));
    }

    [Fact]
    public async Task Refresh_keeps_the_entries_of_other_accounts()
    {
        SeedCache("arbeit");

        await Service().RefreshAsync(Privat, Ct);

        Cache.Load()["arbeit"].ShouldBe(Old);
    }

    [Fact]
    public async Task Refresh_writes_no_token_into_the_cache_file()
    {
        await Service().RefreshAsync(Privat, Ct);

        File.ReadAllText(Paths.UsageCacheFile).ShouldNotContain("sk-ant");
    }

    [Fact]
    public async Task An_expired_token_is_reported_without_a_request()
    {
        SeedCache("privat");
        _credentials.Result = new CredentialReadResult(CredentialReadStatus.Found, new OAuthCredentials(Token, Now));

        var usage = await Service().RefreshAsync(Privat, Ct);

        usage.ShouldBe(new AccountUsage(UsageState.Expired, Old));
        _http.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unknown_expiry_is_tried()
    {
        _credentials.Result = new CredentialReadResult(CredentialReadStatus.Found, new OAuthCredentials(Token, null));

        (await Service().RefreshAsync(Privat, Ct)).State.ShouldBe(UsageState.Ok);
    }

    [Fact]
    public async Task A_missing_login_is_reported_without_a_request()
    {
        _credentials.Result = new CredentialReadResult(CredentialReadStatus.NotLoggedIn, null);

        var usage = await Service().RefreshAsync(Privat, Ct);

        usage.ShouldBe(new AccountUsage(UsageState.NotLoggedIn, null));
        _http.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unreadable_credentials_file_keeps_the_cached_value()
    {
        SeedCache("privat");
        _credentials.Result = new CredentialReadResult(CredentialReadStatus.Error, null);

        var usage = await Service().RefreshAsync(Privat, Ct);

        usage.ShouldBe(new AccountUsage(UsageState.Error, Old));
        _http.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(401, UsageState.Unauthorized)]
    [InlineData(403, UsageState.Unauthorized)]
    [InlineData(429, UsageState.RateLimited)]
    [InlineData(500, UsageState.Error)]
    [InlineData(204, UsageState.Error)]
    public async Task A_failed_request_keeps_the_cached_value(int statusCode, UsageState expected)
    {
        SeedCache("privat");
        _http.Response = new UsageHttpResponse(statusCode, null);

        (await Service().RefreshAsync(Privat, Ct)).ShouldBe(new AccountUsage(expected, Old));
    }

    [Fact]
    public async Task An_unparsable_body_keeps_the_cached_value()
    {
        SeedCache("privat");
        _http.Response = new UsageHttpResponse(200, "<html>rate limited</html>");

        (await Service().RefreshAsync(Privat, Ct)).ShouldBe(new AccountUsage(UsageState.Error, Old));
        Cache.Load()["privat"].ShouldBe(Old);
    }

    [Fact]
    public async Task A_network_failure_keeps_the_cached_value()
    {
        SeedCache("privat");
        _http.OnRequest = _ => throw new HttpRequestException("no route to host");

        (await Service().RefreshAsync(Privat, Ct)).ShouldBe(new AccountUsage(UsageState.Error, Old));
    }

    [Fact]
    public async Task A_request_timeout_keeps_the_cached_value()
    {
        SeedCache("privat");
        _http.OnRequest = _ => throw new TaskCanceledException("timed out");

        (await Service().RefreshAsync(Privat, Ct)).ShouldBe(new AccountUsage(UsageState.Error, Old));
    }

    [Fact]
    public async Task A_second_refresh_within_60_s_repeats_the_last_result_without_a_request()
    {
        var service = Service();
        await service.RefreshAsync(Privat, Ct);
        _time.Advance(TimeSpan.FromSeconds(59));

        var usage = await service.RefreshAsync(Privat, Ct);

        usage.ShouldBe(new AccountUsage(UsageState.Ok, Fresh(Now)));
        _http.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_refresh_after_60_s_asks_again()
    {
        var service = Service();
        await service.RefreshAsync(Privat, Ct);
        _time.Advance(TimeSpan.FromSeconds(60));

        var usage = await service.RefreshAsync(Privat, Ct);

        usage.Snapshot!.FetchedAt.ShouldBe(Now + TimeSpan.FromSeconds(60));
        _http.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task The_minimum_interval_counts_per_account()
    {
        var service = Service();

        await service.RefreshAsync(Privat, Ct);
        await service.RefreshAsync(Arbeit, Ct);

        _http.Requests.Count.ShouldBe(2);
        Cache.Load().Keys.Order().ShouldBe(["arbeit", "privat"]);
    }

    [Fact]
    public async Task Refreshes_of_many_accounts_run_at_the_same_time()
    {
        var accounts = Enumerable.Range(0, 8).Select(i => Privat with { Id = $"account-{i}" }).ToList();
        var service = Service();

        var results = await Task.WhenAll(accounts.Select(a => service.RefreshAsync(a, Ct)));

        results.ShouldAllBe(r => r.State == UsageState.Ok);
        Cache.Load().Count.ShouldBe(8);
    }

    [Fact]
    public async Task A_canceled_refresh_throws_and_writes_nothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => Service().RefreshAsync(Privat, cts.Token));

        File.Exists(Paths.UsageCacheFile).ShouldBeFalse();
    }

    private sealed class FakeCredentialSource : ICredentialSource
    {
        public CredentialReadResult Result { get; set; } = new(CredentialReadStatus.NotLoggedIn, null);

        public Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeUsageHttp : IUsageHttp
    {
        private readonly Lock _gate = new();

        public List<string> Requests { get; } = [];

        public UsageHttpResponse Response { get; set; } = new(200, "{}");

        public Action<string>? OnRequest { get; set; }

        public Task<UsageHttpResponse> GetUsageAsync(string accessToken, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                Requests.Add(accessToken);
            }

            OnRequest?.Invoke(accessToken);
            return Task.FromResult(Response);
        }
    }
}
