using System.Security.Cryptography;
using System.Text;
using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

/// <summary>
/// Reads the usage limits of an account with its Claude Code token. The token is never refreshed and never stored;
/// an expired one simply leaves the last cached value standing. A token of another account than the one
/// <c>.claude.json</c> names shows no limits at all, so a tile never carries someone else's numbers.
/// Every fresh value also goes into the history, and a verified profile into the profile cache.
/// </summary>
public sealed class UsageService(
    ICredentialSource credentials,
    ILoginInfoReader login,
    IUsageHttp http,
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
    private readonly Dictionary<string, CheckedToken> _owners = [];

    // Forget counts up an account's generation, and a refresh only writes back while the generation it started with
    // still holds. A set of forgotten ids would not do: ids are slugs and a re-created account gets the same one.
    private readonly Dictionary<string, int> _generations = [];
    private Dictionary<string, UsageSnapshot>? _snapshots;
    private Dictionary<string, AccountProfile>? _profiles;

    public AccountUsage GetCached(Account account) =>
        Cached(account.Id) is { } snapshot
            ? new AccountUsage(UsageState.Ok, snapshot)
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

    /// <summary>
    /// Best effort: a file that cannot be written or deleted is logged and the rest is removed anyway. A refresh still
    /// running writes nothing back once it ends (see <see cref="_generations"/>).
    /// </summary>
    public void Forget(Account account)
    {
        var id = account.Id;
        lock (_gate)
        {
            _generations[id] = _generations.GetValueOrDefault(id) + 1;
            _attempts.Remove(id);
            _owners.Remove(id);
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
        var read = await credentials.ReadAsync(account, paths, ct).ConfigureAwait(false);
        if (read is not { Status: CredentialReadStatus.Found, Credentials: { } found })
        {
            return new AccountUsage(
                read.Status == CredentialReadStatus.NotLoggedIn ? UsageState.NotLoggedIn : UsageState.Error,
                cached);
        }

        if (found.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return new AccountUsage(UsageState.Expired, cached);
        }

        // Without a login in .claude.json there is nothing to compare with, and the tile says so anyway.
        if (login.Read(paths) is { } expected)
        {
            var check = await ProfileAsync(account.Id, generation, found.AccessToken, ct).ConfigureAwait(false);
            if (check.Profile is not { } profile)
            {
                return new AccountUsage(check.State, cached);
            }

            var owner = profile.ToOwner();
            if (!IsSameAccount(expected, owner))
            {
                // The history and the profile stay: they describe this account, not the stray token.
                ForgetLimits(account.Id, generation);
                return new AccountUsage(UsageState.WrongAccount, null, owner.Email);
            }

            StoreProfile(account.Id, generation, profile);
        }

        var (state, body) = await GetAsync(() => http.GetUsageAsync(found.AccessToken, ct), ct).ConfigureAwait(false);
        if (state != UsageState.Ok)
        {
            return new AccountUsage(state, cached);
        }

        if (body is null || UsageResponseParser.Parse(body, now) is not { } snapshot)
        {
            return new AccountUsage(UsageState.Error, cached);
        }

        Store(account.Id, generation, snapshot);
        return new AccountUsage(UsageState.Ok, snapshot);
    }

    /// <summary>Asks the profile endpoint once per token; only an answer that names the owner is remembered.</summary>
    /// <returns>The profile with <see cref="UsageState.Ok"/>, otherwise no profile and the state of the failure.</returns>
    private async Task<(UsageState State, AccountProfile? Profile)> ProfileAsync(
        string accountId,
        int generation,
        string accessToken,
        CancellationToken ct)
    {
        var tokenHash = Hash(accessToken);
        lock (_gate)
        {
            if (_owners.TryGetValue(accountId, out var known) && known.TokenHash == tokenHash)
            {
                return (UsageState.Ok, known.Profile);
            }
        }

        var (state, body) = await GetAsync(() => http.GetProfileAsync(accessToken, ct), ct).ConfigureAwait(false);
        if (state != UsageState.Ok)
        {
            return (state, null);
        }

        if (body is null || ProfileParser.Parse(body) is not { } profile)
        {
            return (UsageState.Error, null);
        }

        lock (_gate)
        {
            if (IsCurrent(accountId, generation))
            {
                _owners[accountId] = new CheckedToken(tokenHash, profile);
            }
        }

        return (UsageState.Ok, profile);
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

    /// <summary>The uuid decides when both sides have one; older files only carry the e-mail.</summary>
    private static bool IsSameAccount(ClaudeLoginInfo expected, TokenOwner owner) =>
        expected.AccountUuid is { } uuid && owner.AccountUuid is { } ownerUuid
            ? string.Equals(uuid, ownerUuid, StringComparison.OrdinalIgnoreCase)
            : string.Equals(expected.Email, owner.Email, StringComparison.OrdinalIgnoreCase);

    /// <summary>Only the hash is remembered, so the token itself does not outlive the refresh.</summary>
    private static string Hash(string accessToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));

    private UsageSnapshot? Cached(string id)
    {
        lock (_gate)
        {
            return Snapshots().GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// The history append stays under <see cref="_gate"/> too, so Forget cannot slip in between the check and the
    /// write; the history store never calls back into this class.
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
            snapshots[id] = snapshot;
            cache.Save(snapshots);
            history.Append(id, UsageHistoryPoint.From(snapshot));
        }
    }

    /// <summary>
    /// Writes the file only when the profile changed, since every refresh of a known token passes here. A failed
    /// write is logged and undone in memory, so the next refresh sees the profile as changed again and retries;
    /// the profile must never cost the refresh its limits either way.
    /// </summary>
    private void StoreProfile(string id, int generation, AccountProfile profile)
    {
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

    /// <summary>Guarded twin of <see cref="ForgetLimits(string)"/>: skipped once the account was forgotten meanwhile.</summary>
    private void ForgetLimits(string id, int generation)
    {
        lock (_gate)
        {
            if (!IsCurrent(id, generation))
            {
                return;
            }

            var snapshots = Snapshots();
            if (snapshots.Remove(id))
            {
                cache.Save(snapshots);
            }
        }
    }

    /// <summary>Call under <see cref="_gate"/>: the cache file is read once and written from here on.</summary>
    private Dictionary<string, UsageSnapshot> Snapshots() => _snapshots ??= new Dictionary<string, UsageSnapshot>(cache.Load());

    /// <summary>Call under <see cref="_gate"/>, like <see cref="Snapshots"/>.</summary>
    private Dictionary<string, AccountProfile> Profiles() => _profiles ??= new Dictionary<string, AccountProfile>(profiles.Load());

    private sealed record Attempt(DateTimeOffset At, AccountUsage Result);

    private sealed record CheckedToken(string TokenHash, AccountProfile Profile);
}
