using Mcacc.Core.Abstractions;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.App.Services;

/// <summary>
/// Refreshes the limits of every account while the app is open, so the history grows even when nobody clicks.
/// The one-minute guard in <see cref="IUsageProvider.RefreshAsync"/> still applies, except for a
/// <see cref="RecheckAsync"/>.
/// </summary>
public sealed class UsagePoller(
    IAccountService accounts,
    IUsageProvider usage,
    ILog log,
    IReadOnlyList<TimeSpan>? recheckAfter = null,
    TimeSpan? interval = null) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Claude Code renews an expired token only once it runs, a moment after the terminal opened; a login through the
    /// browser takes longer still. Measured from the start.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> RecheckAfter = [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60)];

    private readonly IReadOnlyList<TimeSpan> _recheckAfter = recheckAfter ?? RecheckAfter;
    private readonly TimeSpan _interval = interval ?? Interval;
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

    /// <summary>
    /// After a start of Claude Code: reads the limits of the account again at each <see cref="RecheckAfter"/>, past
    /// the one-minute guard, until one comes back fine. Stops when the account is gone or the poller disposed. Call on
    /// the UI thread, like <see cref="Start"/>.
    /// </summary>
    public async Task RecheckAsync(Account account)
    {
        var ct = _stop.Token;
        var waited = TimeSpan.Zero;
        try
        {
            foreach (var at in _recheckAfter)
            {
                await Task.Delay(at - waited, ct).ConfigureAwait(true);
                waited = at;
                if (!accounts.Config.Accounts.Any(known => known.Id == account.Id))
                {
                    return;
                }

                usage.Invalidate(account);
                if (await RefreshAsync(account, ct).ConfigureAwait(true) is not { } result)
                {
                    continue;
                }

                Updated?.Invoke(account, result);
                if (result.State == UsageState.Ok)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <summary>
    /// MCACC lives on in the tray for days, so a tick that throws, e.g. in an <see cref="Updated"/> handler, is logged
    /// and the next one comes all the same; only the own cancellation ends the loop.
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(true))
            {
                try
                {
                    await TickAsync(ct).ConfigureAwait(true);
                }
                catch (Exception e) when (!ct.IsCancellationRequested)
                {
                    log.Warn("A poll of the limits failed.", e);
                }
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
