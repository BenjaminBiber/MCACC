using System.Text;

namespace Mcacc.Core.Quoting;

public static class PowerShellQuoting
{
    // PowerShell ends a single-quoted string at any of these, and reads a doubled one as that character.
    private const string SingleQuotes = "'\u2018\u2019\u201A\u201B";

    public static string SingleQuote(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('\'');
        foreach (var c in value)
        {
            builder.Append(c);
            if (SingleQuotes.Contains(c))
            {
                builder.Append(c);
            }
        }

        return builder.Append('\'').ToString();
    }
}
