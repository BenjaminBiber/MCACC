namespace Mcacc.Core.Usage;

/// <summary>A limit value at one moment, measured or guessed.</summary>
public readonly record struct UsageValue(DateTimeOffset At, double Value);

/// <summary>
/// Guesses a limit line across a stretch without history points, e.g. while MCACC was not running. Within a window the
/// utilization only grows; at the reset it drops to 0, and the next window begins with the next use, which the reset
/// time of the next point tells: it lies one window length after that beginning.
/// </summary>
public static class UsageGapEstimate
{
    public static readonly TimeSpan FiveHour = TimeSpan.FromHours(5);

    public static readonly TimeSpan SevenDay = TimeSpan.FromDays(7);

    /// <param name="window">The length of the window the values belong to: <see cref="FiveHour"/> or <see cref="SevenDay"/>.</param>
    /// <returns>The corners of the guessed line from <paramref name="a"/> to <paramref name="b"/>, both included.</returns>
    public static IReadOnlyList<UsageValue> Bridge(
        UsageValue a,
        DateTimeOffset? aResetsAt,
        UsageValue b,
        DateTimeOffset? bResetsAt,
        TimeSpan window)
    {
        var nextStart = bResetsAt - window;
        var corners = new List<UsageValue> { a };
        if (aResetsAt is { } reset)
        {
            if (reset > b.At)
            {
                // Still the window of a: the value rose from a to b at some unknown time.
                return [a, b];
            }

            var resetAt = Clamp(reset, a.At, b.At);
            var beginAt = nextStart is { } begin ? Clamp(begin, resetAt, b.At) : resetAt;
            corners.Add(new UsageValue(resetAt, a.Value));
            corners.Add(new UsageValue(resetAt, 0));
            corners.Add(new UsageValue(beginAt, 0));
        }
        else if (nextStart is { } begin)
        {
            // No window was running at a, so nothing was used until the next one began.
            corners.Add(new UsageValue(Clamp(begin, a.At, b.At), a.Value));
        }

        corners.Add(b);
        return WithoutRepeats(corners);
    }

    /// <summary>The part of <paramref name="line"/> from <paramref name="start"/> on; the crossing is interpolated.</summary>
    public static IReadOnlyList<UsageValue> From(IReadOnlyList<UsageValue> line, DateTimeOffset start)
    {
        var kept = new List<UsageValue>();
        for (var i = 0; i < line.Count; i++)
        {
            if (line[i].At < start)
            {
                continue;
            }

            if (kept.Count == 0 && i > 0 && line[i].At > start)
            {
                kept.Add(Interpolate(line[i - 1], line[i], start));
            }

            kept.Add(line[i]);
        }

        return kept;
    }

    private static UsageValue Interpolate(UsageValue before, UsageValue after, DateTimeOffset at)
    {
        var share = (at - before.At) / (after.At - before.At);
        return new UsageValue(at, before.Value + (after.Value - before.Value) * share);
    }

    private static DateTimeOffset Clamp(DateTimeOffset value, DateTimeOffset min, DateTimeOffset max) =>
        value < min ? min : value > max ? max : value;

    /// <summary>A corner equal to the one before it would only draw a line of no length.</summary>
    private static List<UsageValue> WithoutRepeats(List<UsageValue> corners)
    {
        var kept = new List<UsageValue>(corners.Count);
        foreach (var corner in corners)
        {
            if (kept.Count == 0 || kept[^1] != corner)
            {
                kept.Add(corner);
            }
        }

        return kept;
    }
}
