namespace Mcacc.Core.Desktop;

/// <summary>
/// Reads the <c>claude://</c> URL that the operating system puts on the command line when it opens the protocol
/// handler. The login leaves Claude Desktop for the system browser and comes back this way, and the launch carries
/// no <c>--user-data-dir</c>, so the URL decides which profile ends up signed in.
/// </summary>
public static class DesktopDeepLink
{
    /// <returns>False when the command line holds no <c>claude://</c> argument.</returns>
    /// <remarks>The URL carries a one-time login code, so it must never reach a log.</remarks>
    public static bool TryGetUrl(string? commandLine, out string url)
    {
        url = "";
        if (string.IsNullOrEmpty(commandLine))
        {
            return false;
        }

        var scheme = ClaudeDesktopConstants.DeepLinkScheme;
        var start = commandLine.IndexOf(scheme, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return false;
        }

        var rest = commandLine[start..];
        var quoted = start > 0 && commandLine[start - 1] == '"';
        var end = quoted ? rest.IndexOf('"') : IndexOfWhitespace(rest);
        url = end < 0 ? rest : rest[..end];
        return url.Length > scheme.Length;
    }

    private static int IndexOfWhitespace(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
