using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Mcacc.Core.Processes;

/// <summary>Parses the output of <c>ps -axww -o pid=,ppid=,lstart=,command=</c>.</summary>
public static class PsOutputParser
{
    private const int StartTimeTokenCount = 5;
    private const int LeadingTokenCount = 2 + StartTimeTokenCount;
    private const string StartTimeFormat = "ddd MMM d HH:mm:ss yyyy";

    /// <remarks>Malformed lines are skipped.</remarks>
    public static IReadOnlyList<ProcessInfo> Parse(string output)
    {
        var processes = new List<ProcessInfo>();
        foreach (var line in output.Split('\n'))
        {
            if (TryParseLine(line.TrimEnd(), out var process))
            {
                processes.Add(process);
            }
        }

        return processes;
    }

    private static bool TryParseLine(string line, [NotNullWhen(true)] out ProcessInfo? process)
    {
        process = null;
        var tokens = new string[LeadingTokenCount];
        var position = 0;
        for (var i = 0; i < tokens.Length; i++)
        {
            position = SkipWhitespace(line, position);
            var end = position;
            while (end < line.Length && !char.IsWhiteSpace(line[end]))
            {
                end++;
            }

            if (end == position)
            {
                return false;
            }

            tokens[i] = line[position..end];
            position = end;
        }

        var command = line[SkipWhitespace(line, position)..];
        if (command.Length == 0
            || !int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
            || !int.TryParse(tokens[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parentPid))
        {
            return false;
        }

        var exeEnd = command.IndexOf(" --", StringComparison.Ordinal);
        var exePath = exeEnd < 0 ? command : command[..exeEnd].TrimEnd();
        process = new ProcessInfo(pid, parentPid, exePath, command, ParseStartTime(string.Join(' ', tokens[2..])));
        return true;
    }

    private static int SkipWhitespace(string line, int position)
    {
        while (position < line.Length && char.IsWhiteSpace(line[position]))
        {
            position++;
        }

        return position;
    }

    private static DateTimeOffset? ParseStartTime(string text) =>
        DateTime.TryParseExact(text, StartTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local)
            ? new DateTimeOffset(local)
            : null;
}
