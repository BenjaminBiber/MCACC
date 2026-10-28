using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Providers;
using Mcacc.Core.Stats;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Providers;

/// <summary>
/// Each router must hand the call to the implementation of the account's provider and to no other one — a tile
/// showing another provider's numbers, or a start of the wrong program, is the failure these guard against.
/// </summary>
public class RoutingProvidersTests
{
    private static readonly Account Claude = Account.CreateDefault();
    private static readonly Account Codex = new("privat", "Privat", "#6A9BCC", IsDefault: false, ProviderKind.Codex);
    private static readonly AccountPaths Paths =
        new(ProviderDescriptors.Claude, null, null, "dir", "id.json", "creds.json", []);

    [Fact]
    public async Task The_credential_source_of_the_accounts_provider_reads()
    {
        var claude = new RecordingCredentialSource();
        var codex = new RecordingCredentialSource();
        var routing = new RoutingCredentialSource(ProviderMap<ICredentialSource>.Of(claude, codex));

        await routing.ReadAsync(Codex, Paths, TestContext.Current.CancellationToken);

        codex.Accounts.ShouldBe([Codex]);
        claude.Accounts.ShouldBeEmpty();
    }

    [Fact]
    public void The_login_reader_of_the_accounts_provider_reads()
    {
        var claude = new RecordingLoginInfoReader();
        var codex = new RecordingLoginInfoReader();
        var routing = new RoutingLoginInfoReader(ProviderMap<ILoginInfoReader>.Of(claude, codex));

        routing.Read(Claude, Paths);

        claude.Accounts.ShouldBe([Claude]);
        codex.Accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_usage_member_goes_to_the_accounts_provider()
    {
        var claude = new RecordingUsageProvider();
        var codex = new RecordingUsageProvider();
        var routing = new RoutingUsageProvider(ProviderMap<IUsageProvider>.Of(claude, codex));

        routing.GetCached(Codex);
        await routing.RefreshAsync(Codex, TestContext.Current.CancellationToken);
        routing.Invalidate(Codex);
        routing.GetProfile(Codex);
        routing.GetHistory(Codex, DateTimeOffset.UnixEpoch);
        routing.Forget(Codex);

        codex.Calls.ShouldBe(["GetCached", "RefreshAsync", "Invalidate", "GetProfile", "GetHistory", "Forget"]);
        claude.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_stats_member_goes_to_the_accounts_provider()
    {
        var claude = new RecordingStatsProvider();
        var codex = new RecordingStatsProvider();
        var routing = new RoutingStatsProvider(ProviderMap<IStatsProvider>.Of(claude, codex));

        await routing.LoadAsync(Claude, null, TestContext.Current.CancellationToken);
        routing.LoadSummary(Claude);
        routing.Forget(Claude);

        claude.Calls.ShouldBe(["LoadAsync", "LoadSummary", "Forget"]);
        codex.Calls.ShouldBeEmpty();
    }

    /// <summary>A gap must be loud: picking Claude for a Codex account would read the wrong files.</summary>
    [Fact]
    public void A_router_without_an_implementation_for_the_provider_throws()
    {
        var routing = new RoutingUsageProvider(ProviderMap<IUsageProvider>.Of(new RecordingUsageProvider()));

        Should.Throw<NotSupportedException>(() => routing.GetCached(Codex));
    }

    private sealed class RecordingCredentialSource : ICredentialSource
    {
        public List<Account> Accounts { get; } = [];

        public Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct)
        {
            Accounts.Add(account);
            return Task.FromResult(new CredentialReadResult(CredentialReadStatus.NotLoggedIn, null));
        }
    }

    private sealed class RecordingLoginInfoReader : ILoginInfoReader
    {
        public List<Account> Accounts { get; } = [];

        public AccountLoginInfo? Read(Account account, AccountPaths paths)
        {
            Accounts.Add(account);
            return null;
        }
    }

    private sealed class RecordingUsageProvider : IUsageProvider
    {
        public List<string> Calls { get; } = [];

        public AccountUsage GetCached(Account account)
        {
            Calls.Add(nameof(GetCached));
            return new AccountUsage(UsageState.NotLoggedIn, null);
        }

        public Task<AccountUsage> RefreshAsync(Account account, CancellationToken ct)
        {
            Calls.Add(nameof(RefreshAsync));
            return Task.FromResult(new AccountUsage(UsageState.NotLoggedIn, null));
        }

        public void Invalidate(Account account) => Calls.Add(nameof(Invalidate));

        public AccountProfile? GetProfile(Account account)
        {
            Calls.Add(nameof(GetProfile));
            return null;
        }

        public IReadOnlyList<UsageHistoryPoint> GetHistory(Account account, DateTimeOffset since)
        {
            Calls.Add(nameof(GetHistory));
            return [];
        }

        public void Forget(Account account) => Calls.Add(nameof(Forget));
    }

    private sealed class RecordingStatsProvider : IStatsProvider
    {
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<TokenBucket>> LoadAsync(Account account, IProgress<double>? progress, CancellationToken ct)
        {
            Calls.Add(nameof(LoadAsync));
            return Task.FromResult<IReadOnlyList<TokenBucket>>([]);
        }

        public IReadOnlyList<DailyTokens> LoadSummary(Account account)
        {
            Calls.Add(nameof(LoadSummary));
            return [];
        }

        public void Forget(Account account) => Calls.Add(nameof(Forget));
    }
}
