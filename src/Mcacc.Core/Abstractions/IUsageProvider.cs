using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.Core.Abstractions;

public interface IUsageProvider
{
    /// <summary>The last known value, without any IO.</summary>
    AccountUsage GetCached(Account account);

    Task<AccountUsage> RefreshAsync(Account account, CancellationToken ct);

    /// <summary>The last profile the endpoint reported for the account's own token; null when none is known.</summary>
    AccountProfile? GetProfile(Account account);

    /// <summary>The history points from <paramref name="since"/> on, oldest first.</summary>
    IReadOnlyList<UsageHistoryPoint> GetHistory(Account account, DateTimeOffset since);

    /// <summary>For a removed account: drops its limits, profile and history.</summary>
    void Forget(Account account);
}
