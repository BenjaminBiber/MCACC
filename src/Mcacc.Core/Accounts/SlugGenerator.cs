using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Mcacc.Core.Accounts;

public static partial class SlugGenerator
{
    private const int MaxLength = 32;
    private const string Fallback = "account";

    private static readonly (string From, string To)[] Transliterations =
        [("ä", "ae"), ("ö", "oe"), ("ü", "ue"), ("ß", "ss"), ("æ", "ae"), ("ø", "oe"), ("å", "aa")];

    private static readonly HashSet<string> Reserved =
    [
        "standard", "default", "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    ];

    public static string Generate(string name, Func<string, bool> isTaken)
    {
        var candidate = Candidate(name);
        var slug = candidate;
        for (var suffix = 2; Reserved.Contains(slug) || isTaken(slug); suffix++)
        {
            slug = candidate + "-" + suffix.ToString(CultureInfo.InvariantCulture);
        }

        return slug;
    }

    private static string Candidate(string name)
    {
        var text = name.Trim().ToLowerInvariant();
        foreach (var (from, to) in Transliterations)
        {
            text = text.Replace(from, to, StringComparison.Ordinal);
        }

        text = WithoutMarks(text.Normalize(NormalizationForm.FormKD));
        text = DisallowedRun().Replace(text, "-").Trim('-');
        if (text.Length > MaxLength)
        {
            text = text[..MaxLength].TrimEnd('-');
        }

        return text.Length == 0 ? Fallback : text;
    }

    private static string WithoutMarks(string decomposed)
    {
        var result = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex DisallowedRun();
}
