using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.Core.Abstractions;

public interface IUsageProvider
{
    /// <summary>The last known value, without any IO.</summary>
    AccountUsage GetCached(Account account);

    Task<AccountUsage> RefreshAsync(Account account, CancellationToken ct);
}
