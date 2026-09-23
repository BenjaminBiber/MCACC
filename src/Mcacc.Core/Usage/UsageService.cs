using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

/// <summary>
/// Reads the usage limits of an account with its Claude Code token. The token is never refreshed and never stored;
/// an expired one simply leaves the last cached value standing.
/// </summary>
public sealed class UsageService(
    ICredentialSource credentials,
    IUsageHttp http,
    UsageCache cache,
    AccountPathResolver resolver,
    TimeProvider? time = null)
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(60);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Attempt> _attempts = [];
    private Dictionary<string, UsageSnapshot>? _snapshots;

    public AccountUsage GetCached(Account account) =>
        Cached(account.Id) is { } snapshot
            ? new AccountUsage(UsageState.Ok, snapshot)
            : new AccountUsage(UsageState.NotLoggedIn, null);

    /// <summary>Asks the endpoint at most once per minute and account; in between it repeats the last result.</summary>
    public async Task<AccountUsage> RefreshAsync(Account account, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (_attempts.TryGetValue(account.Id, out var last) && now - last.At < MinimumInterval)
            {
                return last.Result;
            }
        }

        var usage = await FetchAsync(account, now, ct).ConfigureAwait(false);
        lock (_gate)
        {
            _attempts[account.Id] = new Attempt(now, usage);
        }

        return usage;
    }

    private async Task<AccountUsage> FetchAsync(Account account, DateTimeOffset now, CancellationToken ct)
    {
        var cached = Cached(account.Id);
        var read = await credentials.ReadAsync(account, resolver.Resolve(account), ct).ConfigureAwait(false);
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

        UsageHttpResponse response;
        try
        {
            response = await http.GetUsageAsync(found.AccessToken, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new AccountUsage(UsageState.Error, cached);
        }

        var state = response.StatusCode switch
        {
            401 or 403 => UsageState.Unauthorized,
            429 => UsageState.RateLimited,
            200 => UsageState.Ok,
            _ => UsageState.Error,
        };
        if (state != UsageState.Ok)
        {
            return new AccountUsage(state, cached);
        }

        if (response.Body is null || UsageResponseParser.Parse(response.Body, now) is not { } snapshot)
        {
            return new AccountUsage(UsageState.Error, cached);
        }

        Store(account.Id, snapshot);
        return new AccountUsage(UsageState.Ok, snapshot);
    }

    private UsageSnapshot? Cached(string id)
    {
        lock (_gate)
        {
            return Snapshots().GetValueOrDefault(id);
        }
    }

    private void Store(string id, UsageSnapshot snapshot)
    {
        lock (_gate)
        {
            var snapshots = Snapshots();
            snapshots[id] = snapshot;
            cache.Save(snapshots);
        }
    }

    /// <summary>Call under <see cref="_gate"/>: the cache file is read once and written from here on.</summary>
    private Dictionary<string, UsageSnapshot> Snapshots() => _snapshots ??= new Dictionary<string, UsageSnapshot>(cache.Load());

    private sealed record Attempt(DateTimeOffset At, AccountUsage Result);
}
