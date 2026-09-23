using System.Text;

namespace Mcacc.Core.Paths;

public static class PathComparer
{
    public static string Normalize(string path, PathStyle style)
    {
        var separator = style.Separator();
        var unified = style == PathStyle.Windows ? path.Replace('/', '\\') : path;

        var result = new StringBuilder(unified.Length);
        foreach (var c in unified)
        {
            if (c == separator && result.Length > 0 && result[^1] == separator)
            {
                continue;
            }

            result.Append(c);
        }

        if (style == PathStyle.Windows && unified.StartsWith(@"\\", StringComparison.Ordinal))
        {
            // Restore the UNC prefix that the collapsing above reduced to one separator.
            result.Insert(0, separator);
        }

        var rootLength = RootLength(result.ToString(), style);
        while (result.Length > rootLength && result[^1] == separator)
        {
            result.Length--;
        }

        return result.ToString();
    }

    public static bool Same(string a, string b, PathStyle style) =>
        string.Equals(Normalize(a, style), Normalize(b, style), StringComparison.OrdinalIgnoreCase);

    public static bool IsStrictlyUnder(string child, string parent, PathStyle style)
    {
        var resolvedChild = Resolve(child, style);
        var resolvedParent = Resolve(parent, style);
        if (resolvedParent.Length == 0
            || resolvedChild.Length <= resolvedParent.Length
            || !resolvedChild.StartsWith(resolvedParent, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separator = style.Separator();
        return resolvedParent[^1] == separator || resolvedChild[resolvedParent.Length] == separator;
    }

    private static string Resolve(string path, PathStyle style)
    {
        var normalized = Normalize(path, style);
        var rootLength = RootLength(normalized, style);
        var segments = new List<string>();
        foreach (var segment in normalized[rootLength..].Split(style.Separator(), StringSplitOptions.RemoveEmptyEntries))
        {
            switch (segment)
            {
                case ".":
                    break;
                case ".." when segments.Count > 0 && segments[^1] != "..":
                    segments.RemoveAt(segments.Count - 1);
                    break;
                case ".." when rootLength > 0:
                    // A rooted path cannot climb above its root.
                    break;
                default:
                    segments.Add(segment);
                    break;
            }
        }

        return normalized[..rootLength] + string.Join(style.Separator(), segments);
    }

    private static int RootLength(string normalized, PathStyle style)
    {
        if (style == PathStyle.Posix)
        {
            return normalized.StartsWith('/') ? 1 : 0;
        }

        if (normalized.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return 2;
        }

        if (normalized.StartsWith('\\'))
        {
            return 1;
        }

        if (normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':')
        {
            return normalized.Length >= 3 && normalized[2] == '\\' ? 3 : 2;
        }

        return 0;
    }
}
