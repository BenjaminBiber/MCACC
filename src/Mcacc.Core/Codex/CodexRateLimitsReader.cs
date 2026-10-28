using System.Text;

namespace Mcacc.Core.Codex;

/// <summary>
/// The newest rate limits Codex has written to disk. The session files are partitioned by date and their names
/// start with an iso timestamp, so newest-first is a sort, not a scan: no file is stat-ed for its time and none is
/// parsed from the front. Within a file the search runs backwards from the end, because the limits of a session
/// stand on its last lines.
/// </summary>
public sealed class CodexRateLimitsReader(int maxDays = 14, long maxBytesPerFile = 1 << 20)
{
    private const string SessionsFolder = "sessions";
    private const int BlockSize = 64 * 1024;

    /// <returns>Null when no session file below <paramref name="codexHome"/> carries rate limits.</returns>
    public CodexRateLimits? Read(string codexHome)
    {
        var days = 0;
        foreach (var day in DayDirectories(Path.Join(codexHome, SessionsFolder)))
        {
            if (++days > maxDays)
            {
                return null;
            }

            if (NewestInDay(day) is { } limits)
            {
                return limits;
            }
        }

        return null;
    }

    /// <summary>Year, month and day directories, each numerically descending, so the newest day comes first.</summary>
    private static IEnumerable<string> DayDirectories(string sessions)
    {
        foreach (var year in NumericChildren(sessions))
        {
            foreach (var month in NumericChildren(year))
            {
                foreach (var day in NumericChildren(month))
                {
                    yield return day;
                }
            }
        }
    }

    private static IEnumerable<string> NumericChildren(string directory)
    {
        string[] children;
        try
        {
            children = Directory.GetDirectories(directory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }

        return children
            .Select(path => (Directory: path, Number: Number(Path.GetFileName(path))))
            .Where(child => child.Number is not null)
            .OrderByDescending(child => child.Number!.Value)
            .Select(child => child.Directory);
    }

    private static int? Number(string name) => int.TryParse(name, out var number) ? number : null;

    private CodexRateLimits? NewestInDay(string day)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(day, "rollout-*.jsonl");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }

        // The name embeds the start time, so ordinal descending is chronological — and needs no clock.
        Array.Sort(files, static (left, right) => string.CompareOrdinal(right, left));
        foreach (var file in files)
        {
            if (NewestInFile(file) is { } limits)
            {
                return limits;
            }
        }

        return null;
    }

    private CodexRateLimits? NewestInFile(string file)
    {
        try
        {
            // Codex keeps the file of a running session open, so it is shared for writing and deleting.
            using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return NewestIn(stream);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private CodexRateLimits? NewestIn(Stream stream)
    {
        var floor = Math.Max(0, stream.Length - maxBytesPerFile);
        var position = stream.Length;
        var buffer = new byte[BlockSize];

        // Whatever a block cuts off at its end belongs to the line that continues into the block after it.
        var tail = Array.Empty<byte>();
        while (position > floor)
        {
            var length = (int)Math.Min(BlockSize, position - floor);
            position -= length;
            stream.Position = position;
            stream.ReadExactly(buffer, 0, length);

            var block = Combine(buffer.AsSpan(0, length), tail);
            var start = position > floor ? IndexAfterFirstNewline(block) : 0;
            if (start < 0)
            {
                tail = block;
                continue;
            }

            if (NewestInBlock(block, start) is { } limits)
            {
                return limits;
            }

            tail = block[..Math.Max(start, 0)];
        }

        return null;
    }

    /// <summary>Walks the block's complete lines from the last to the first.</summary>
    private static CodexRateLimits? NewestInBlock(byte[] block, int start)
    {
        var end = block.Length;
        for (var i = block.Length - 1; i >= start; i--)
        {
            if (block[i] != (byte)'\n')
            {
                continue;
            }

            if (Line(block, i + 1, end) is { } limits)
            {
                return limits;
            }

            end = i;
        }

        return Line(block, start, end);
    }

    private static CodexRateLimits? Line(byte[] block, int from, int to)
    {
        var span = block.AsSpan(from, Math.Max(0, to - from));
        return span.IndexOf(Marker) < 0 ? null : CodexRateLimitsParser.ParseRolloutLine(Encoding.UTF8.GetString(span));
    }

    private static readonly byte[] Marker = Encoding.UTF8.GetBytes(CodexRateLimitsParser.RolloutMarker);

    private static int IndexAfterFirstNewline(byte[] block)
    {
        var newline = Array.IndexOf(block, (byte)'\n');
        return newline < 0 ? -1 : newline + 1;
    }

    private static byte[] Combine(ReadOnlySpan<byte> head, byte[] tail)
    {
        if (tail.Length == 0)
        {
            return head.ToArray();
        }

        var combined = new byte[head.Length + tail.Length];
        head.CopyTo(combined);
        tail.CopyTo(combined, head.Length);
        return combined;
    }
}
