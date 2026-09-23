using System.Text.RegularExpressions;

namespace Mcacc.Core.Model;

public static partial class AccountPalette
{
    public static IReadOnlyList<string> Colors { get; } =
        ["#D97757", "#6A9BCC", "#7D9B76", "#9B7BA8", "#C2A477", "#5E8C8A", "#B5667B", "#6B6F76"];

    public static string NextFreeColor(IEnumerable<string> used)
    {
        var taken = new HashSet<string>(used, StringComparer.OrdinalIgnoreCase);
        return Colors.FirstOrDefault(color => !taken.Contains(color)) ?? Colors[0];
    }

    public static bool IsValidColor(string? color) => color is not null && ColorPattern().IsMatch(color);

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}\z")]
    private static partial Regex ColorPattern();
}
