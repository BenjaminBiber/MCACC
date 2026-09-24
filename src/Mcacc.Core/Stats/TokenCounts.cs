using System.Text.Json.Serialization;

namespace Mcacc.Core.Stats;

/// <summary>
/// The tokens of one response or the sum of many. Cache writes are split by their lifetime because the two are
/// priced differently.
/// </summary>
public sealed record TokenCounts(long Input, long Output, long CacheWrite5Min, long CacheWrite1Hour, long CacheRead)
{
    public static readonly TokenCounts Zero = new(0, 0, 0, 0, 0);

    [JsonIgnore]
    public long Total => Input + Output + CacheWrite5Min + CacheWrite1Hour + CacheRead;

    [JsonIgnore]
    public long CacheWrite => CacheWrite5Min + CacheWrite1Hour;

    public static TokenCounts operator +(TokenCounts left, TokenCounts right) => new(
        left.Input + right.Input,
        left.Output + right.Output,
        left.CacheWrite5Min + right.CacheWrite5Min,
        left.CacheWrite1Hour + right.CacheWrite1Hour,
        left.CacheRead + right.CacheRead);
}
