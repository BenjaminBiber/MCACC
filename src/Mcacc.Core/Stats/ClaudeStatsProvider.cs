using System.Collections.Concurrent;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Stats;

public sealed class ClaudeStatsProvider(StatsIndexStore store, AccountPathResolver resolver) : IClaudeStatsProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    /// <summary>One scan per account at a time; a second caller waits and then finds the index up to date.</summary>
    public async Task<IReadOnlyList<TokenBucket>> LoadAsync(
        Account account,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var gate = _gates.GetOrAdd(account.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var claudeDir = resolver.Resolve(account).EffectiveClaudeDir;
            return await Task.Run(() => Scan(account.Id, claudeDir, gate, progress, ct), ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Forget(Account account)
    {
        _gates.TryRemove(account.Id, out _);
        store.Delete(account.Id);
    }

    private IReadOnlyList<TokenBucket> Scan(
        string accountId,
        string claudeDir,
        SemaphoreSlim gate,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var result = ClaudeLogScanner.Update(claudeDir, store.Load(accountId), progress, ct);

        // A forgotten account has lost its gate; writing now would bring its index back.
        if (result.Changed && _gates.TryGetValue(accountId, out var current) && ReferenceEquals(current, gate))
        {
            try
            {
                store.Save(accountId, result.Index);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The numbers are still right; the next load just scans the same bytes again.
            }
        }

        return result.Index.Files.SelectMany(file => file.Buckets).ToList();
    }
}
