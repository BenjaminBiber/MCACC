using Mcacc.Core.Abstractions;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.App.Services;

/// <summary>
/// Refreshes the limits of every account while the app is open, so the history grows even when nobody clicks.
/// The one-minute guard in <see cref="IUsageProvider.RefreshAsync"/> still applies.
/// </summary>
public sealed class UsagePoller(IAccountService accounts, IUsageProvider usage, ILog log) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly CancellationTokenSource _stop = new();

    /// <summary>Raised per account after a tick, on the thread that called <see cref="Start"/>.</summary>
    public event Action<Account, AccountUsage>? Updated;

    /// <summary>Call on the UI thread: the loop keeps its context, so the pages get their updates there.</summary>
    public void Start() => _ = RunAsync(_stop.Token);

    public async Task TickAsync(CancellationToken ct)
    {
        var all = accounts.Config.Accounts;
        var results = await Task.WhenAll(all.Select(account => RefreshAsync(account, ct))).ConfigureAwait(true);
        for (var i = 0; i < all.Count; i++)
        {
            if (results[i] is { } result)
            {
                Updated?.Invoke(all[i], result);
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(true))
            {
                await TickAsync(ct).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }

    /// <returns>Null when the refresh failed; the failure is logged and the next tick tries again.</returns>
    private async Task<AccountUsage?> RefreshAsync(Account account, CancellationToken ct)
    {
        try
        {
            return await usage.RefreshAsync(account, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.Warn($"Polling the limits of '{account.Id}' failed.", e);
            return null;
        }
    }
}
