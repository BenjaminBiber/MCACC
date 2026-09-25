using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Logging;
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

    private const string OwnUuid = "00000000-0000-0000-0000-000000000001";

    private const string OtherProfile =
        """{ "account": { "uuid": "00000000-0000-0000-0000-000000000099", "email": "privat@example.com" } }""";

    private static readonly ClaudeLoginInfo OwnLogin = new("max.muster@example.com", null, null, OwnUuid);

    private static readonly UsageSnapshot Old = new(
        new UsageWindow(11, new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero)),
        null,
        Now - TimeSpan.FromHours(3));

    private readonly TempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(Now, TimeZoneInfo.Utc);
    private readonly FakeCredentialSource _credentials = new();
    private readonly FakeLoginReader _login = new();
    private readonly FakeUsageHttp _http = new();
    private readonly RecordingLog _log = new();

    public UsageServiceTests()
    {
        _credentials.Result = new CredentialReadResult(
            CredentialReadStatus.Found,
            new OAuthCredentials(Token, Now + TimeSpan.FromHours(1)));
        _http.Response = new UsageHttpResponse(200, FixtureFiles.ReadAllText("usage-ok.json"));
        _http.ProfileResponse = new UsageHttpResponse(200, FixtureFiles.ReadAllText("profile-ok.json"));
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

    private ProfileCache Profiles => new(Paths);

    private UsageHistoryStore History => new(Paths, _time);

    private UsageService Service() =>
        new(_credentials, _login, _http, Cache, Profiles, History, new AccountPathResolver(Paths, Platform), _time, _log);

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
    public async Task A_token_renewed_within_60_s_shows_after_an_invalidate()
    {
        _credentials.Result = new CredentialReadResult(
            CredentialReadStatus.Found,
            new OAuthCredentials(Token, Now - TimeSpan.FromMinutes(1)));
        var service = Service();
        (await service.RefreshAsync(Privat, Ct)).State.ShouldBe(UsageState.Expired);
        _credentials.Result = new CredentialReadResult(
            CredentialReadStatus.Found,
            new OAuthCredentials(Token, Now + TimeSpan.FromHours(8)));
        _time.Advance(TimeSpan.FromSeconds(15));

        service.Invalidate(Privat);
        var usage = await service.RefreshAsync(Privat, Ct);

        usage.State.ShouldBe(UsageState.Ok);
        usage.Snapshot!.FetchedAt.ShouldBe(Now + TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Invalidating_one_account_keeps_the_guard_of_the_others()
    {
        var service = Service();
        await service.RefreshAsync(Privat, Ct);
        await service.RefreshAsync(Arbeit, Ct);

        service.Invalidate(Arbeit);
        await service.RefreshAsync(Privat, Ct);

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
    public async Task A_token_of_the_logged_in_account_is_checked_and_then_read()
    {
        _login.Info = OwnLogin;

        var usage = await Service().RefreshAsync(Privat, Ct);

        usage.ShouldBe(new AccountUsage(UsageState.Ok, Fresh(Now)));
        _http.ProfileRequests.ShouldBe([Token]);
        _http.Requests.ShouldBe([Token]);
        _login.ReadPaths.Single().ClaudeJsonFile
            .ShouldBe(new AccountPathResolver(Paths, Platform).Resolve(Privat).ClaudeJsonFile);
    }

    [Fact]
    public async Task A_token_of_another_account_shows_no_limits_and_forgets_the_cached_ones()
    {
        SeedCache("privat", "arbeit");
        _login.Info = OwnLogin;
        _http.ProfileResponse = new UsageHttpResponse(200, OtherProfile);

        var usage = await Service().RefreshAsync(Privat, Ct);

        usage.ShouldBe(new AccountUsage(UsageState.WrongAccount, null, "privat@example.com"));
        _http.Requests.ShouldBeEmpty();
        Cache.Load().Keys.ShouldBe(["arbeit"]);
    }

    [Fact]
    public async Task Without_a_uuid_the_emails_are_compared_ignoring_case()
    {
        _login.Info = new ClaudeLoginInfo("Max.Muster@example.com", null, null);
        _http.ProfileResponse = new UsageHttpResponse(200, """{ "account": { "email": "max.muster@EXAMPLE.com" } }""");

        (await Service().RefreshAsync(Privat, Ct)).State.ShouldBe(UsageState.Ok);
    }

    [Fact]
    public async Task The_owner_of_a_token_is_asked_only_once()
    {
        _login.Info = OwnLogin;
        var service = Service();
        await service.RefreshAsync(Privat, Ct);
        _time.Advance(TimeSpan.FromSeconds(60));

        await service.RefreshAsync(Privat, Ct);

        _http.ProfileRequests.Count.ShouldBe(1);
        _http.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_new_token_is_checked_again()
    {
        const string rotated = Token + "-ROTATED";
        _login.Info = OwnLogin;
        var service = Service();
        await service.RefreshAsync(Privat, Ct);
        _time.Advance(TimeSpan.FromSeconds(60));
        _credentials.Result = new CredentialReadResult(
            CredentialReadStatus.Found,
            new OAuthCredentials(rotated, Now + TimeSpan.FromHours(1)));

        await service.RefreshAsync(Privat, Ct);

        _http.ProfileRequests.ShouldBe([Token, rotated]);
    }

    [Fact]
    public async Task Without_a_login_the_token_is_not_checked()
    {
        (await Service().RefreshAsync(Privat, Ct)).State.ShouldBe(UsageState.Ok);

        _http.ProfileRequests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(401, UsageState.Unauthorized)]
    [InlineData(403, UsageState.Unauthorized)]
    [InlineData(429, UsageState.RateLimited)]
    [InlineData(500, UsageState.Error)]
    public async Task A_failed_owner_check_keeps_the_cached_value(int statusCode, UsageState expected)
    {
        SeedCache("privat");
        _login.Info = OwnLogin;
        _http.ProfileResponse = new UsageHttpResponse(statusCode, null);

        (await Service().RefreshAsync(Privat, Ct)).ShouldBe(new AccountUsage(expected, Old));
        _http.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unparsable_profile_keeps_the_cached_value()
    {
        SeedCache("privat");
        _login.Info = OwnLogin;
        _http.ProfileResponse = new UsageHttpResponse(200, "<html>oops</html>");

        (await Service().RefreshAsync(Privat, Ct)).ShouldBe(new AccountUsage(UsageState.Error, Old));
        _http.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_network_failure_during_the_owner_check_keeps_the_cached_value()
    {
        SeedCache("privat");
        _login.Info = OwnLogin;
        _http.OnProfileRequest = _ => throw new HttpRequestException("no route to host");

        (await Service().RefreshAsync(Privat, Ct)).ShouldBe(new AccountUsage(UsageState.Error, Old));
    }

    [Fact]
    public async Task A_failed_owner_check_is_not_remembered()
    {
        _login.Info = OwnLogin;
        var profile = _http.ProfileResponse;
        _http.ProfileResponse = new UsageHttpResponse(500, null);
        var service = Service();
        await service.RefreshAsync(Privat, Ct);
        _time.Advance(TimeSpan.FromSeconds(60));
        _http.ProfileResponse = profile;

        (await service.RefreshAsync(Privat, Ct)).State.ShouldBe(UsageState.Ok);
        _http.ProfileRequests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_canceled_refresh_throws_and_writes_nothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => Service().RefreshAsync(Privat, cts.Token));

        File.Exists(Paths.UsageCacheFile).ShouldBeFalse();
    }

    [Fact]
    public async Task A_fresh_snapshot_is_appended_to_the_history()
    {
        await Service().RefreshAsync(Privat, Ct);

        History.Read("privat", DateTimeOffset.MinValue).ShouldBe([UsageHistoryPoint.From(Fresh(Now))]);
    }

    [Fact]
    public async Task A_failed_request_adds_nothing_to_the_history()
    {
        _http.Response = new UsageHttpResponse(500, null);

        await Service().RefreshAsync(Privat, Ct);

        History.Read("privat", DateTimeOffset.MinValue).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_profile_of_the_own_token_is_cached()
    {
        _login.Info = OwnLogin;

        await Service().RefreshAsync(Privat, Ct);

        Service().GetProfile(Privat).ShouldBe(ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json")));
    }

    [Fact]
    public void Without_a_cached_profile_there_is_none()
    {
        Service().GetProfile(Privat).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_of_another_account_keeps_the_history_and_the_profile()
    {
        var own = ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json"))!;
        Profiles.Save(new Dictionary<string, AccountProfile> { ["privat"] = own });
        History.Append("privat", UsageHistoryPoint.From(Old));
        _login.Info = OwnLogin;
        _http.ProfileResponse = new UsageHttpResponse(200, OtherProfile);

        (await Service().RefreshAsync(Privat, Ct)).State.ShouldBe(UsageState.WrongAccount);

        Service().GetProfile(Privat).ShouldBe(own);
        History.Read("privat", DateTimeOffset.MinValue).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Forget_drops_the_limits_the_profile_and_the_history()
    {
        _login.Info = OwnLogin;
        var service = Service();
        await service.RefreshAsync(Privat, Ct);

        service.Forget(Privat);

        service.GetCached(Privat).State.ShouldBe(UsageState.NotLoggedIn);
        service.GetProfile(Privat).ShouldBeNull();
        Cache.Load().ShouldBeEmpty();
        Profiles.Load().ShouldBeEmpty();
        History.Read("privat", DateTimeOffset.MinValue).ShouldBeEmpty();
    }

    [Fact]
    public async Task After_forget_the_next_refresh_asks_again()
    {
        var service = Service();
        await service.RefreshAsync(Privat, Ct);

        service.Forget(Privat);
        await service.RefreshAsync(Privat, Ct);

        _http.Requests.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_refresh_that_ends_after_forget_writes_nothing_back(bool duringOwnerCheck)
    {
        _login.Info = OwnLogin;
        var service = Service();
        if (duringOwnerCheck)
        {
            _http.OnProfileRequest = _ => service.Forget(Privat);
        }
        else
        {
            _http.OnRequest = _ => service.Forget(Privat);
        }

        await service.RefreshAsync(Privat, Ct);

        service.GetCached(Privat).State.ShouldBe(UsageState.NotLoggedIn);
        service.GetProfile(Privat).ShouldBeNull();
        service.GetHistory(Privat, DateTimeOffset.MinValue).ShouldBeEmpty();
        Cache.Load().ShouldNotContainKey("privat");
        Profiles.Load().ShouldNotContainKey("privat");
        File.Exists(Paths.UsageHistoryFile("privat")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_wrong_account_forget_does_not_erase_a_recreated_accounts_snapshot()
    {
        _login.Info = OwnLogin;
        var ownProfileResponse = _http.ProfileResponse;
        _http.ProfileResponse = new UsageHttpResponse(200, OtherProfile);
        var service = Service();
        _http.OnProfileRequest = _ =>
        {
            // Simulate the account being removed and re-added under the same slug while this
            // wrong-account request is still in flight: the re-created account gets a fresh snapshot.
            _http.OnProfileRequest = null;
            service.Forget(Privat);
            _http.ProfileResponse = ownProfileResponse;
            service.RefreshAsync(Privat, Ct).GetAwaiter().GetResult();
        };

        var usage = await service.RefreshAsync(Privat, Ct);

        usage.State.ShouldBe(UsageState.WrongAccount);
        Cache.Load()["privat"].ShouldBe(Fresh(Now));
    }

    [Fact]
    public async Task An_account_created_again_under_a_forgotten_id_refreshes_normally()
    {
        _login.Info = OwnLogin;
        var service = Service();
        _http.OnRequest = _ => service.Forget(Privat);
        await service.RefreshAsync(Privat, Ct);
        _http.OnRequest = null;

        var usage = await service.RefreshAsync(Privat, Ct);

        usage.ShouldBe(new AccountUsage(UsageState.Ok, Fresh(Now)));
        _http.Requests.Count.ShouldBe(2);
        service.GetProfile(Privat).ShouldBe(ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json")));
        Cache.Load()["privat"].ShouldBe(Fresh(Now));
        History.Read("privat", DateTimeOffset.MinValue).ShouldBe([UsageHistoryPoint.From(Fresh(Now))]);
    }

    [Fact]
    public async Task Forget_goes_on_when_the_profile_cache_cannot_be_written()
    {
        _login.Info = OwnLogin;
        var service = Service();
        await service.RefreshAsync(Privat, Ct);
        BlockWrites(Paths.ProfileCacheFile);

        service.Forget(Privat);

        Cache.Load().ShouldBeEmpty();
        File.Exists(Paths.UsageHistoryFile("privat")).ShouldBeFalse();
        _log.Warnings.ShouldHaveSingleItem().Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_failed_profile_save_is_retried_on_the_next_refresh()
    {
        _login.Info = OwnLogin;
        BlockWrites(Paths.ProfileCacheFile);
        var service = Service();
        await service.RefreshAsync(Privat, Ct);
        service.GetProfile(Privat).ShouldBeNull();
        _time.Advance(TimeSpan.FromSeconds(60));
        Directory.Delete(Paths.ProfileCacheFile + ".tmp");

        await service.RefreshAsync(Privat, Ct);

        Profiles.Load()["privat"].ShouldBe(ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json")));
        _log.Warnings.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_profile_cache_that_cannot_be_written_still_lets_the_limits_through()
    {
        _login.Info = OwnLogin;
        BlockWrites(Paths.ProfileCacheFile);

        var usage = await Service().RefreshAsync(Privat, Ct);

        usage.ShouldBe(new AccountUsage(UsageState.Ok, Fresh(Now)));
        _http.Requests.ShouldBe([Token]);
        _log.Warnings.ShouldHaveSingleItem().Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetHistory_returns_the_points_since_the_given_time()
    {
        History.Append("privat", UsageHistoryPoint.From(Old));

        await Service().RefreshAsync(Privat, Ct);

        Service().GetHistory(Privat, Now - TimeSpan.FromHours(1)).ShouldBe([UsageHistoryPoint.From(Fresh(Now))]);
    }

    /// <summary>A directory where the temp file of a json write goes makes that write fail on every platform.</summary>
    private static void BlockWrites(string file) => Directory.CreateDirectory(file + ".tmp");

    private sealed class FakeCredentialSource : ICredentialSource
    {
        public CredentialReadResult Result { get; set; } = new(CredentialReadStatus.NotLoggedIn, null);

        public Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeLoginReader : ILoginInfoReader
    {
        private readonly Lock _gate = new();

        public ClaudeLoginInfo? Info { get; set; }

        public List<AccountPaths> ReadPaths { get; } = [];

        public ClaudeLoginInfo? Read(AccountPaths paths)
        {
            lock (_gate)
            {
                ReadPaths.Add(paths);
            }

            return Info;
        }
    }

    private sealed class FakeUsageHttp : IUsageHttp
    {
        private readonly Lock _gate = new();

        public List<string> Requests { get; } = [];

        public List<string> ProfileRequests { get; } = [];

        public UsageHttpResponse Response { get; set; } = new(200, "{}");

        public UsageHttpResponse ProfileResponse { get; set; } = new(200, "{}");

        public Action<string>? OnRequest { get; set; }

        public Action<string>? OnProfileRequest { get; set; }

        public Task<UsageHttpResponse> GetUsageAsync(string accessToken, CancellationToken ct) =>
            Record(Requests, OnRequest, Response, accessToken, ct);

        public Task<UsageHttpResponse> GetProfileAsync(string accessToken, CancellationToken ct) =>
            Record(ProfileRequests, OnProfileRequest, ProfileResponse, accessToken, ct);

        private Task<UsageHttpResponse> Record(
            List<string> requests,
            Action<string>? onRequest,
            UsageHttpResponse response,
            string accessToken,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                requests.Add(accessToken);
            }

            onRequest?.Invoke(accessToken);
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingLog : ILog
    {
        private readonly Lock _gate = new();

        public List<(string Message, Exception? Exception)> Warnings { get; } = [];

        public void Info(string message)
        {
        }

        public void Warn(string message, Exception? exception = null)
        {
            lock (_gate)
            {
                Warnings.Add((message, exception));
            }
        }

        public void Error(string message, Exception? exception = null) => Warn(message, exception);
    }
}
