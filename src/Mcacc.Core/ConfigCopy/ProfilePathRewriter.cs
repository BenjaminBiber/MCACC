using System.Text.RegularExpressions;
using Mcacc.Core.Paths;

namespace Mcacc.Core.ConfigCopy;

/// <summary>
/// Points the paths inside a copied file at the new profile. Without it a copy keeps working against the profile it
/// came from — and for Codex that is not cosmetic: <c>config.toml</c> sets <c>CODEX_HOME</c> itself, so an
/// unrewritten copy would quietly undo the isolation of the account it was just created for.
/// </summary>
/// <remarks>
/// Every spelling is replaced by its own kind, so a match inside a quoted string keeps that string's escaping. That
/// matters because one file can mix both: a real <c>config.toml</c> has single-quoted literals with plain
/// backslashes next to double-quoted basic strings with doubled ones.
/// </remarks>
public static class ProfilePathRewriter
{
    // A reference ends at the end of the text, a separator, a quote, whitespace, ")", "," or ";",
    // so ".claude.json" and ".claude-x" are not references.
    private const string Boundary = """(?=\z|[/\\"'\s),;])""";

    public static string Rewrite(string text, AccountPaths source, AccountPaths target)
    {
        var sourceDir = source.EffectiveConfigDir;
        ArgumentException.ThrowIfNullOrEmpty(sourceDir);
        var targetDir = target.EffectiveConfigDir;

        List<(string Find, string Replace)> spellings =
        [
            (Escaped(sourceDir), Escaped(targetDir)),
            (sourceDir, targetDir),
            (ForwardSlashed(sourceDir), ForwardSlashed(targetDir)),
        ];

        // The account that uses the provider's own home is also written as ~ or $HOME in hand-edited files.
        if (source.ConfigDirOverride is null)
        {
            var home = source.Provider.Cli.DefaultHomeFolder;
            spellings.Add(($"~/{home}", ForwardSlashed(targetDir)));
            spellings.Add(($"$HOME/{home}", ForwardSlashed(targetDir)));
        }

        // Posix paths have no backslashes, so their spellings coincide.
        var distinct = spellings.DistinctBy(s => s.Find, StringComparer.OrdinalIgnoreCase).ToList();
        var alternatives = string.Join('|', distinct.Select(s => $"({Regex.Escape(s.Find)})"));
        var pattern = new Regex(
            $"(?:{alternatives}){Boundary}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // One pass, so a replacement is never matched again by another spelling.
        return pattern.Replace(text, match => distinct[MatchedGroupIndex(match)].Replace);
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

    /// <summary>As a json string or a toml basic string spells it: every backslash doubled.</summary>
    private static string Escaped(string path) => path.Replace(@"\", @"\\", StringComparison.Ordinal);

    private static string ForwardSlashed(string path) => path.Replace('\\', '/');
}
