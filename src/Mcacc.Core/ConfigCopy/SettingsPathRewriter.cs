using System.Text.RegularExpressions;

namespace Mcacc.Core.ConfigCopy;

public static class SettingsPathRewriter
{
    // A reference ends at the end of the text, a separator, a quote, whitespace, ")", "," or ";",
    // so ".claude.json" and ".claude-x" are not references.
    private const string Boundary = """(?=\z|[/\\"'\s),;])""";

    public static string Rewrite(string json, string sourceClaudeDir, string targetClaudeDir, bool sourceIsDefault)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceClaudeDir);

        List<(string Find, string Replace)> spellings =
        [
            (sourceClaudeDir, targetClaudeDir),
            (JsonEscaped(sourceClaudeDir), JsonEscaped(targetClaudeDir)),
            (ForwardSlashed(sourceClaudeDir), ForwardSlashed(targetClaudeDir)),
        ];
        if (sourceIsDefault)
        {
            spellings.Add(("~/.claude", ForwardSlashed(targetClaudeDir)));
            spellings.Add(("$HOME/.claude", ForwardSlashed(targetClaudeDir)));
        }

        // Posix paths have no backslashes, so their three spellings coincide.
        var distinct = spellings.DistinctBy(s => s.Find, StringComparer.OrdinalIgnoreCase).ToList();
        var alternatives = string.Join('|', distinct.Select(s => $"({Regex.Escape(s.Find)})"));
        var pattern = new Regex(
            $"(?:{alternatives}){Boundary}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // One pass, so a replacement is never matched again by another spelling.
        return pattern.Replace(json, match => distinct[MatchedGroupIndex(match)].Replace);
    }

    private static int MatchedGroupIndex(Match match)
    {
        for (var i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success)
            {
                return i - 1;
            }
        }

        throw new InvalidOperationException("A match always comes from one of the spellings.");
    }

    private static string JsonEscaped(string path) => path.Replace(@"\", @"\\", StringComparison.Ordinal);

    private static string ForwardSlashed(string path) => path.Replace('\\', '/');
}
