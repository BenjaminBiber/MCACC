namespace Mcacc.Core.Processes;

/// <summary>Reads <c>--name=value</c> switches from a Windows command line or from macOS <c>ps</c> output.</summary>
public static class CommandLineSwitch
{
    // ps prints argv unquoted, so an unquoted value may hold spaces; it ends where the next Chromium
    // (" --x") or Windows (" /x", e.g. crashpad's "/prefetch:4") style argument starts.
    private static readonly string[] NextArgumentMarkers = [" --", " /"];

    /// <returns>False if the switch is absent or its value is empty.</returns>
    public static bool TryGetValue(string? commandLine, string name, out string value)
    {
        value = "";
        if (string.IsNullOrEmpty(commandLine))
        {
            return false;
        }

        var prefix = name + "=";
        var start = IndexOfSwitch(commandLine, prefix);
        if (start < 0)
        {
            return false;
        }

        var valueStart = start + prefix.Length;
        var wholeArgumentQuoted = start > 0 && commandLine[start - 1] == '"';
        string raw;
        if (wholeArgumentQuoted || (valueStart < commandLine.Length && commandLine[valueStart] == '"'))
        {
            var open = wholeArgumentQuoted ? valueStart : valueStart + 1;
            var close = commandLine.IndexOf('"', open);
            raw = close < 0 ? commandLine[open..] : commandLine[open..close];
        }
        else
        {
            var rest = commandLine[valueStart..];
            raw = rest[..IndexOfNextArgument(rest)];
        }

        value = TrimQuotesAndWhitespace(raw);
        return value.Length > 0;
    }

    private static int IndexOfSwitch(string commandLine, string prefix)
    {
        for (var i = commandLine.IndexOf(prefix, StringComparison.Ordinal);
             i >= 0;
             i = commandLine.IndexOf(prefix, i + 1, StringComparison.Ordinal))
        {
            if (i == 0 || char.IsWhiteSpace(commandLine[i - 1]) || commandLine[i - 1] == '"')
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfNextArgument(string rest)
    {
        var end = rest.Length;
        foreach (var marker in NextArgumentMarkers)
        {
            var index = rest.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0 && index < end)
            {
                end = index;
            }
        }

        return end;
    }

    private static string TrimQuotesAndWhitespace(string raw)
    {
        var span = raw.AsSpan();
        while (span.Length > 0 && (char.IsWhiteSpace(span[0]) || span[0] == '"'))
        {
            span = span[1..];
        }

        while (span.Length > 0 && (char.IsWhiteSpace(span[^1]) || span[^1] == '"'))
        {
            span = span[..^1];
        }

        return span.ToString();
    }
}
