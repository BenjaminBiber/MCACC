using System.Text.Json;
using Mcacc.Core.Config;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

/// <summary>
/// The limit history of each account, one json object per line. Lines are only ever appended; the first append of a
/// UTC day also drops the points older than <see cref="Retention"/>.
/// </summary>
public sealed class UsageHistoryStore(McaccPaths paths, TimeProvider? time = null)
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateOnly> _prunedOn = [];

    /// <summary>A failed write is dropped: the history must never cost the caller its fresh limits.</summary>
    public void Append(string accountId, UsageHistoryPoint point)
    {
        var file = paths.UsageHistoryFile(accountId);
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(paths.UsageHistoryDir);
                PruneOncePerDay(accountId, file);
                File.AppendAllText(file, Line(point));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The next refresh appends again; a gap in the history is all that is lost.
            }
        }
    }

    public IReadOnlyList<UsageHistoryPoint> Read(string accountId, DateTimeOffset since)
    {
        lock (_gate)
        {
            return ReadAll(paths.UsageHistoryFile(accountId))
                .Where(point => point.At >= since)
                .OrderBy(point => point.At)
                .ToList();
        }
    }

    public void Delete(string accountId)
    {
        lock (_gate)
        {
            _prunedOn.Remove(accountId);
            var file = paths.UsageHistoryFile(accountId);
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    private void PruneOncePerDay(string accountId, string file)
    {
        var now = _time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (_prunedOn.GetValueOrDefault(accountId) == today)
        {
            return;
        }

        _prunedOn[accountId] = today;
        var all = ReadAll(file);
        var kept = all.Where(point => point.At >= now - Retention).ToList();
        if (kept.Count == all.Count)
        {
            return;
        }

        var tempFile = file + ".tmp";
        File.WriteAllText(tempFile, string.Concat(kept.Select(Line)));
        File.Move(tempFile, file, overwrite: true);
    }

    /// <summary>A broken line, e.g. from a crash mid-write, is skipped; a missing file reads as empty.</summary>
    private static List<UsageHistoryPoint> ReadAll(string file)
    {
        var points = new List<UsageHistoryPoint>();
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    if (JsonSerializer.Deserialize(line, McaccCompactJsonContext.Default.UsageHistoryPoint) is { } point)
                    {
                        points.Add(point);
                    }
                }
                catch (JsonException)
                {
                    // Skipped, see the summary.
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Whatever was read so far stands.
        }

        return points;
    }

    private static string Line(UsageHistoryPoint point) =>
        JsonSerializer.Serialize(point, McaccCompactJsonContext.Default.UsageHistoryPoint) + "\n";
}
