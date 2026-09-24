using Mcacc.Core.Model;
using Mcacc.Core.Stats;

namespace Mcacc.Core.Abstractions;

public interface IClaudeStatsProvider
{
    /// <summary>Brings the index up to date with the account's session logs and returns all its buckets.</summary>
    /// <param name="progress">0 to 1 over the bytes still to read; a first scan can take a while.</param>
    Task<IReadOnlyList<TokenBucket>> LoadAsync(Account account, IProgress<double>? progress, CancellationToken ct);

    /// <summary>For a removed account: deletes its index.</summary>
    void Forget(Account account);
}
