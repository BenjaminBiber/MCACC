using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.Core.Providers;

/// <summary>
/// Keeps the one <see cref="IUsageProvider"/> the app is wired with while each provider reads its limits its own way.
/// </summary>
public sealed class RoutingUsageProvider(ProviderMap<IUsageProvider> providers) : IUsageProvider
{
    public AccountUsage GetCached(Account account) => providers.For(account).GetCached(account);

    public Task<AccountUsage> RefreshAsync(Account account, CancellationToken ct) =>
        providers.For(account).RefreshAsync(account, ct);

    public void Invalidate(Account account) => providers.For(account).Invalidate(account);

    public AccountProfile? GetProfile(Account account) => providers.For(account).GetProfile(account);

    public IReadOnlyList<UsageHistoryPoint> GetHistory(Account account, DateTimeOffset since) =>
        providers.For(account).GetHistory(account, since);

    public void Forget(Account account) => providers.For(account).Forget(account);
}
