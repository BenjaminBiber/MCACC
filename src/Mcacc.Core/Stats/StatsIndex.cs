namespace Mcacc.Core.Stats;

/// <summary>What the scanner already knows about the session logs of one account, kept in <c>stats/&lt;id&gt;.json</c>.</summary>
/// <param name="Version">Raised whenever the meaning of a field changes; an index of another version is rebuilt.</param>
public sealed record StatsIndex(int Version, IReadOnlyList<IndexedLogFile> Files)
{
    public const int CurrentVersion = 1;

    public static readonly StatsIndex Empty = new(CurrentVersion, []);
}

/// <summary>One log file as the last scan left it.</summary>
/// <param name="Path">Relative to <c>&lt;claudeDir&gt;/projects</c>.</param>
/// <param name="Offset">The byte right after the last complete line; the next scan of a grown file starts here.</param>
/// <param name="Ids">The responses this file counted, so no other file counts them again.</param>
public sealed record IndexedLogFile(
    string Path,
    long Length,
    DateTime LastWriteUtc,
    long Offset,
    IReadOnlyList<TokenBucket> Buckets,
    IReadOnlyList<long> Ids,
    int BrokenLines);

public sealed record LogScanResult(StatsIndex Index, bool Changed);
