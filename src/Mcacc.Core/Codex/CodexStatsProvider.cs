using System.Collections.Concurrent;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Stats;

namespace Mcacc.Core.Codex;

/// <summary>
/// The token statistics of a Codex account, out of its session files. Codex keeps no pre-computed daily summary the
/// way Claude Code does, so <see cref="LoadSummary"/> has nothing to add and the calendar shows what the retained
/// sessions say.
/// </summary>
public sealed class CodexStatsProvider(StatsIndexStore store, AccountPathResolver resolver) : IStatsProvider
{
    private const string SessionsFolder = "sessions";

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    // Forget's delete and a scan's post-scan write touch the same file; see ClaudeStatsProvider for the reasoning.
    private readonly Lock _writeGate = new();

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
            var root = Path.Join(resolver.Resolve(account).EffectiveConfigDir, SessionsFolder);
            return await Task.Run(() => Scan(account.Id, root, gate, progress, ct), ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <remarks>Codex writes no summary of its own, so days whose session files are gone are gone.</remarks>
    public IReadOnlyList<DailyTokens> LoadSummary(Account account) => [];

    public void Forget(Account account)
    {
        lock (_writeGate)
        {
            _gates.TryRemove(account.Id, out _);
            store.Delete(account.Id);
        }
    }

    private IReadOnlyList<TokenBucket> Scan(
        string accountId,
        string root,
        SemaphoreSlim gate,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var result = LogScanner.Update(
            root,
            store.Load(accountId),
            static () => new CodexRolloutLineReader(),
            progress,
            ct);

        if (result.Changed)
        {
            lock (_writeGate)
            {
                if (_gates.TryGetValue(accountId, out var current) && ReferenceEquals(current, gate))
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
            }
        }

        return result.Index.Files.SelectMany(file => file.Buckets).ToList();
    }
}
