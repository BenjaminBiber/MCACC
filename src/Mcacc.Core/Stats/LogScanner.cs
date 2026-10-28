using System.Buffers;
using System.IO.Enumeration;

namespace Mcacc.Core.Stats;

/// <summary>
/// Brings a <see cref="StatsIndex"/> up to date with the session logs below a root directory. Only what is
/// new since the last scan is read, so after the first scan an update takes milliseconds. The format is the
/// <see cref="ILogLineReader"/>.s business; everything here is the same for every provider.
/// </summary>
public static class LogScanner
{
    private const int BufferSize = 64 * 1024;
    private const long ProgressStep = 8L * 1024 * 1024;

    private static readonly EnumerationOptions AllLogs = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

    public static LogScanResult Update(
        string root,
        StatsIndex index,
        Func<ILogLineReader> newReader,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var rebuild = index.Version != StatsIndex.CurrentVersion;

        // Entries of deleted files stay: Claude Code cleans up old transcripts, and their tokens still count.
        var entries = rebuild
            ? new Dictionary<string, IndexedLogFile>(StringComparer.Ordinal)
            : index.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);

        var work = new List<WorkItem>();
        if (Directory.Exists(root))
        {
            foreach (var log in Logs(root).OrderBy(file => file.FullName, StringComparer.Ordinal))
            {
                var path = Path.GetRelativePath(root, log.FullName);
                var old = entries.GetValueOrDefault(path);
                if (old is not null && old.Length == log.Length && old.LastWriteUtc == log.LastWriteTimeUtc)
                {
                    continue;
                }

                work.Add(new WorkItem(log, path, old, Resume: old is not null && log.Length >= old.Length));
            }
        }

        if (work.Count == 0 && !rebuild)
        {
            progress?.Report(1);
            return new LogScanResult(index, Changed: false);
        }

        var seen = entries.Values.SelectMany(file => file.Ids).ToHashSet();
        var counter = new ProgressCounter(work.Sum(item => item.Info.Length - item.Start), progress);
        foreach (var item in work)
        {
            ct.ThrowIfCancellationRequested();
            if (Scan(item, newReader(), seen, counter, ct) is { } scanned)
            {
                entries[item.Path] = scanned;
            }
        }

        progress?.Report(1);
        var files = entries.Values.OrderBy(file => file.Path, StringComparer.Ordinal).ToList();
        return new LogScanResult(new StatsIndex(StatsIndex.CurrentVersion, files), Changed: true);
    }

    /// <returns>The new entry, or null when the file could not be read; the old entry then stands.</returns>
    private static IndexedLogFile? Scan(
        WorkItem item,
        ILogLineReader reader,
        HashSet<long> seen,
        ProgressCounter counter,
        CancellationToken ct)
    {
        var resumed = item.Resume ? item.Old : null;
        reader.State = resumed?.Reader ?? LogReaderState.None;

        // A file read from the start may count its own earlier responses again; they are in seen because of it.
        HashSet<long> own = item.Old is not null && !item.Resume ? [.. item.Old.Ids] : [];
        var sums = new Dictionary<BucketKey, (TokenCounts Tokens, int Messages)>();
        foreach (var bucket in resumed?.Buckets ?? [])
        {
            sums[new BucketKey(bucket.Hour, bucket.Model, bucket.Project, bucket.Session)] = (bucket.Tokens, bucket.Messages);
        }

        var ids = new List<long>(resumed?.Ids ?? []);
        var found = new HashSet<long>();
        var broken = resumed?.BrokenLines ?? 0;
        long offset;
        try
        {
            offset = ReadLines(item.Info.FullName, item.Start, counter, ct, line =>
            {
                if (!reader.TryRead(line, out var usage))
                {
                    broken++;
                    return;
                }

                if (usage is null || found.Contains(usage.Id) || (seen.Contains(usage.Id) && !own.Contains(usage.Id)))
                {
                    return;
                }

                found.Add(usage.Id);
                ids.Add(usage.Id);
                var key = new BucketKey(Hour(usage.Timestamp), usage.Model, usage.Project, usage.Session);
                (TokenCounts Tokens, int Messages) sum = sums.TryGetValue(key, out var known) ? known : (TokenCounts.Zero, 0);
                sums[key] = (sum.Tokens + usage.Tokens, sum.Messages + 1);
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Locked or deleted mid-read: the next scan tries again.
            return null;
        }

        seen.ExceptWith(own);
        seen.UnionWith(found);
        var buckets = sums
            .Select(pair => new TokenBucket(pair.Key.Hour, pair.Key.Model, pair.Key.Project, pair.Key.Session, pair.Value.Tokens, pair.Value.Messages))
            .OrderBy(bucket => bucket.Hour)
            .ThenBy(bucket => bucket.Model, StringComparer.Ordinal)
            .ThenBy(bucket => bucket.Project, StringComparer.Ordinal)
            .ThenBy(bucket => bucket.Session, StringComparer.Ordinal)
            .ToList();
        return new IndexedLogFile(
            item.Path,
            item.Info.Length,
            item.Info.LastWriteTimeUtc,
            offset,
            buckets,
            ids,
            broken,
            reader.State);
    }

    /// <summary>Hands every complete line to <paramref name="onLine"/>; a line may be longer than the buffer.</summary>
    /// <returns>The offset right after the last complete line.</returns>
    private static long ReadLines(
        string file,
        long start,
        ProgressCounter counter,
        CancellationToken ct,
        Action<ReadOnlyMemory<byte>> onLine)
    {
        // Claude Code keeps writing the log of a running session; sharing write and delete lets it.
        using var stream = new FileStream(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 0,
            FileOptions.SequentialScan);
        stream.Seek(start, SeekOrigin.Begin);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var carry = new ArrayBufferWriter<byte>();
        var position = start;
        var consumed = start;
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, BufferSize)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                var chunk = buffer.AsMemory(0, read);
                int newline;
                while ((newline = chunk.Span.IndexOf((byte)'\n')) >= 0)
                {
                    if (carry.WrittenCount == 0)
                    {
                        onLine(chunk[..newline]);
                    }
                    else
                    {
                        carry.Write(chunk.Span[..newline]);
                        onLine(carry.WrittenMemory);
                        carry.ResetWrittenCount();
                    }

                    chunk = chunk[(newline + 1)..];
                }

                carry.Write(chunk.Span);
                position += read;
                consumed = position - carry.WrittenCount;
                counter.Add(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return consumed;
    }

    /// <summary>Iterates the log files, skipping any folder <see cref="LogFiles"/> cannot read.</summary>
    private static IEnumerable<FileInfo> Logs(string root)
    {
        using var logs = new LogFiles(root);
        while (logs.MoveNext())
        {
            yield return logs.Current;
        }
    }

    private static DateTimeOffset Hour(DateTimeOffset at) =>
        new(at.UtcTicks - at.UtcTicks % TimeSpan.TicksPerHour, TimeSpan.Zero);

    private sealed record WorkItem(FileInfo Info, string Path, IndexedLogFile? Old, bool Resume)
    {
        public long Start => Resume ? Old!.Offset : 0;
    }

    private readonly record struct BucketKey(DateTimeOffset Hour, string Model, string Project, string Session);

    /// <summary>
    /// The <c>*.jsonl</c> files under the root. <see cref="EnumerationOptions.IgnoreInaccessible"/> only skips folders
    /// that deny access; this skips any folder that cannot be read.
    /// </summary>
    private sealed class LogFiles(string root) : FileSystemEnumerator<FileInfo>(root, AllLogs)
    {
        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
            !entry.IsDirectory && entry.FileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);

        protected override FileInfo TransformEntry(ref FileSystemEntry entry) => (FileInfo)entry.ToFileSystemInfo();

        // One folder the file system reports as corrupt must not cost all the statistics; the index keeps the counts
        // of its files, as for deleted logs.
        protected override bool ContinueOnError(int error) => true;
    }

    /// <summary>Reports every 8 MB, so a first scan of hundreds of megabytes moves the bar without flooding it.</summary>
    private sealed class ProgressCounter(long total, IProgress<double>? progress)
    {
        private long _read;
        private long _reported;

        public void Add(long bytes)
        {
            _read += bytes;
            if (progress is null || _read - _reported < ProgressStep)
            {
                return;
            }

            _reported = _read;
            progress.Report(total <= 0 ? 1 : Math.Min(1, (double)_read / total));
        }
    }
}
