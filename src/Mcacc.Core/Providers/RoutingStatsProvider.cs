using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Stats;

namespace Mcacc.Core.Providers;

public sealed class RoutingStatsProvider(ProviderMap<IStatsProvider> providers) : IStatsProvider
{
    public Task<IReadOnlyList<TokenBucket>> LoadAsync(Account account, IProgress<double>? progress, CancellationToken ct) =>
        providers.For(account).LoadAsync(account, progress, ct);

    public IReadOnlyList<DailyTokens> LoadSummary(Account account) => providers.For(account).LoadSummary(account);

    public void Forget(Account account) => providers.For(account).Forget(account);
}
