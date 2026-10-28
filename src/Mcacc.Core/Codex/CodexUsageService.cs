using Mcacc.Core.Abstractions;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Usage;

namespace Mcacc.Core.Codex;

/// <summary>
/// Reads the usage limits of a Codex account. The endpoint is asked first; when it cannot be reached the limits
/// Codex last wrote into its session files stand in, marked as local so the tile shows how old they are instead of
/// pretending they are current. The token is never refreshed and never stored.
/// </summary>
public sealed class CodexUsageService(
    ICodexUsageHttp http,
    CodexRateLimitsReader files,
    UsageCache cache,
    ProfileCache profiles,
    UsageHistoryStore history,
    AccountPathResolver resolver,
    TimeProvider? time = null,
    ILog? log = null) : IUsageProvider
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(60);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ILog _log = log ?? NullLog.Instance;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Attempt> _attempts = [];

    // Like UsageService: a refresh only writes back while the generation it started with still holds, because ids
    // are slugs and a re-created account gets the same one.
    private readonly Dictionary<string, int> _generations = [];
    private Dictionary<string, UsageSnapshot>? _snapshots;
    private Dictionary<string, AccountProfile>? _profileCache;

    public AccountUsage GetCached(Account account) =>
        Cached(account.Id) is { } snapshot
            ? new AccountUsage(Snapshot(snapshot), snapshot)
            : new AccountUsage(UsageState.NotLoggedIn, null);

    public AccountProfile? GetProfile(Account account)
    {
        lock (_gate)
        {
            return Profiles().GetValueOrDefault(account.Id);
        }
    }

    public IReadOnlyList<UsageHistoryPoint> GetHistory(Account account, DateTimeOffset since) =>
        history.Read(account.Id, since);

    public void Invalidate(Account account)
    {
        lock (_gate)
        {
            _attempts.Remove(account.Id);
        }
    }

    /// <summary>Best effort: a file that cannot be written or deleted is logged and the rest goes anyway.</summary>
    public void Forget(Account account)
    {
        var id = account.Id;
        lock (_gate)
        {
            _generations[id] = _generations.GetValueOrDefault(id) + 1;
            _attempts.Remove(id);
            var known = Profiles();
            if (known.Remove(id))
            {
                BestEffort($"Forgetting the profile of '{id}' failed.", () => profiles.Save(known));
            }
        }

        BestEffort($"Forgetting the limits of '{id}' failed.", () => ForgetLimits(id));
        BestEffort($"Forgetting the usage history of '{id}' failed.", () => history.Delete(id));
    }

    /// <summary>Asks the endpoint at most once per minute and account; in between it repeats the last result.</summary>
    public async Task<AccountUsage> RefreshAsync(Account account, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        int generation;
        lock (_gate)
        {
            if (_attempts.TryGetValue(account.Id, out var last) && now - last.At < MinimumInterval)
            {
                return last.Result;
            }

            generation = _generations.GetValueOrDefault(account.Id);
        }

        var usage = await FetchAsync(account, generation, now, ct).ConfigureAwait(false);
        lock (_gate)
        {
            if (IsCurrent(account.Id, generation))
            {
                _attempts[account.Id] = new Attempt(now, usage);
            }
        }

        return usage;
    }

    private async Task<AccountUsage> FetchAsync(Account account, int generation, DateTimeOffset now, CancellationToken ct)
    {
        var cached = Cached(account.Id);
        var paths = resolver.Resolve(account);
        var (outcome, auth) = CodexAuthFileReader.Read(paths.CredentialsFile);
        if (outcome == CodexAuthFileReader.Outcome.Missing)
        {
            return new AccountUsage(UsageState.NotLoggedIn, cached);
        }

        if (outcome != CodexAuthFileReader.Outcome.Found || auth is null)
        {
            return new AccountUsage(UsageState.Error, cached);
        }

        // A login with an API key carries no OAuth token, so there is nothing to ask the endpoint with.
        if (auth.AccessToken is not { } token)
        {
            return new AccountUsage(UsageState.NotLoggedIn, cached);
        }

        var live = auth.AccessTokenExpiresAt is { } expiresAt && expiresAt <= now
            ? (UsageState.Expired, (string?)null)
            : await GetAsync(() => http.GetUsageAsync(token, auth.AccountId, ct), ct).ConfigureAwait(false);

        if (live is (UsageState.Ok, { } body) && CodexRateLimitsParser.ParseLive(body, now) is { } fresh)
        {
            if (Mismatch(auth, body) is { } otherEmail)
            {
                // The history and the profile stay: they describe this account, not the stray token.
                ForgetLimits(account.Id, generation);
                return new AccountUsage(UsageState.WrongAccount, null, otherEmail);
            }

            StoreProfile(account.Id, generation, auth, body);
            var snapshot = fresh.ToSnapshot(UsageSource.Live);
            Store(account.Id, generation, snapshot);
            return new AccountUsage(UsageState.Ok, snapshot);
        }

        return FromFiles(account.Id, generation, paths, live.Item1, cached);
    }

    /// <summary>
    /// The fallback. The snapshot carries the timestamp of the session line, not the moment it was read, so its
    /// age is visible and the history gets it at the right place on the time axis.
    /// </summary>
    private AccountUsage FromFiles(
        string id,
        int generation,
        AccountPaths paths,
        UsageState liveState,
        UsageSnapshot? cached)
    {
        if (files.Read(paths.EffectiveConfigDir) is not { } local)
        {
            return new AccountUsage(liveState == UsageState.Ok ? UsageState.Error : liveState, cached);
        }

        var snapshot = local.ToSnapshot(UsageSource.LocalFile);
        Store(id, generation, snapshot);
        return new AccountUsage(UsageState.Stale, snapshot);
    }

    /// <returns>The e-mail the reply names when it is not the one <c>auth.json</c> names, otherwise null.</returns>
    private static string? Mismatch(CodexAuth auth, string body)
    {
        if (CodexUsageOwnerParser.Parse(body) is not { } owner || auth.Login is not { } expected)
        {
            return null;
        }

        if (expected.AccountUuid is { } uuid && owner.AccountUuid is { } ownerUuid)
        {
            return string.Equals(uuid, ownerUuid, StringComparison.OrdinalIgnoreCase) ? null : owner.Email;
        }

        return expected.Email is { } email
               && owner.Email is { } ownerEmail
               && !string.Equals(email, ownerEmail, StringComparison.OrdinalIgnoreCase)
            ? owner.Email
            : null;
    }

    /// <summary>A request that does not reach the endpoint counts as an error; a canceled one still throws.</summary>
    private static async Task<(UsageState State, string? Body)> GetAsync(
        Func<Task<UsageHttpResponse>> request,
        CancellationToken ct)
    {
        UsageHttpResponse response;
        try
        {
            response = await request().ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return (UsageState.Error, null);
        }

        var state = response.StatusCode switch
        {
            401 or 403 => UsageState.Unauthorized,
            429 => UsageState.RateLimited,
            200 => UsageState.Ok,
            _ => UsageState.Error,
        };
        return (state, response.Body);
    }

    private static UsageState Snapshot(UsageSnapshot snapshot) =>
        snapshot.Source == UsageSource.LocalFile ? UsageState.Stale : UsageState.Ok;

    private UsageSnapshot? Cached(string id)
    {
        lock (_gate)
        {
            return Snapshots().GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// Writes nothing when the value is not newer than the one already known. Without that the five-minute poller
    /// would append the same session line to the history for as long as Codex is not used.
    /// </summary>
    private void Store(string id, int generation, UsageSnapshot snapshot)
    {
        lock (_gate)
        {
            if (!IsCurrent(id, generation))
            {
                return;
            }

            var snapshots = Snapshots();
            if (snapshots.GetValueOrDefault(id) is { } known && known.FetchedAt >= snapshot.FetchedAt)
            {
                return;
            }

            snapshots[id] = snapshot;
            cache.Save(snapshots);
            history.Append(id, UsageHistoryPoint.From(snapshot));
        }
    }

    /// <summary>
    /// What the reply and <c>auth.json</c> agree on about the owner. A failed write is logged and undone in memory,
    /// so the next refresh sees the profile as changed again and retries.
    /// </summary>
    private void StoreProfile(string id, int generation, CodexAuth auth, string body)
    {
        var owner = CodexUsageOwnerParser.Parse(body);
        var login = auth.Login;
        if (owner is null && login is null)
        {
            return;
        }

        var profile = new AccountProfile(
            AccountUuid: owner?.AccountUuid ?? login?.AccountUuid,
            Email: owner?.Email ?? login?.Email,
            FullName: login?.DisplayName,
            DisplayName: login?.DisplayName,
            HasClaudeMax: null,
            HasClaudePro: null,
            CreatedAt: null,
            OrganizationName: login?.OrganizationName,
            OrganizationType: null,
            BillingType: null,
            RateLimitTier: owner?.PlanType ?? login?.PlanType);

        lock (_gate)
        {
            var known = Profiles();
            var previous = known.GetValueOrDefault(id);
            if (!IsCurrent(id, generation) || Equals(previous, profile))
            {
                return;
            }

            known[id] = profile;
            if (!BestEffort($"Storing the profile of '{id}' failed.", () => profiles.Save(known)))
            {
                if (previous is null)
                {
                    known.Remove(id);
                }
                else
                {
                    known[id] = previous;
                }
            }
        }
    }

    /// <summary>Call under <see cref="_gate"/>: false once the account was forgotten after the refresh began.</summary>
    private bool IsCurrent(string id, int generation) => _generations.GetValueOrDefault(id) == generation;

    private bool BestEffort(string failure, Action write)
    {
        try
        {
            write();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.Warn(failure, e);
            return false;
        }
    }

    private void ForgetLimits(string id)
    {
        lock (_gate)
        {
            var snapshots = Snapshots();
            if (snapshots.Remove(id))
            {
                cache.Save(snapshots);
            }
        }
    }

    /// <summary>Guarded twin of <see cref="ForgetLimits(string)"/>: skipped once the account was forgotten.</summary>
    private void ForgetLimits(string id, int generation)
    {
        lock (_gate)
        {
            if (IsCurrent(id, generation))
            {
                var snapshots = Snapshots();
                if (snapshots.Remove(id))
                {
                    cache.Save(snapshots);
                }
            }
        }
    }

    /// <summary>Call under <see cref="_gate"/>: the cache file is read once and written from here on.</summary>
    private Dictionary<string, UsageSnapshot> Snapshots() => _snapshots ??= new Dictionary<string, UsageSnapshot>(cache.Load());

    /// <summary>Call under <see cref="_gate"/>, like <see cref="Snapshots"/>.</summary>
    private Dictionary<string, AccountProfile> Profiles() =>
        _profileCache ??= new Dictionary<string, AccountProfile>(profiles.Load());

    private sealed record Attempt(DateTimeOffset At, AccountUsage Result);
}
