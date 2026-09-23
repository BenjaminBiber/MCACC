using System.Text;

namespace Mcacc.Core.Paths;

public static class PathStyleExtensions
{
    private static readonly char[] WindowsSeparators = ['\\', '/'];
    private static readonly char[] PosixSeparators = ['/'];

    public static char Separator(this PathStyle style) => style == PathStyle.Windows ? '\\' : '/';

    public static string Join(this PathStyle style, params string[] parts)
    {
        // Windows also accepts '/', so both are trimmed at the joints there.
        var separators = style == PathStyle.Windows ? WindowsSeparators : PosixSeparators;
        var result = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            if (result.Length == 0)
            {
                result.Append(part);
                continue;
            }

            while (result.Length > 0 && separators.Contains(result[^1]))
            {
                result.Length--;
            }

            result.Append(style.Separator()).Append(part.TrimStart(separators));
        }

        return result.ToString();
    }
}
