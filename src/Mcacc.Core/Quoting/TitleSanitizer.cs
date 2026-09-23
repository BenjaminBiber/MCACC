using System.Text;

namespace Mcacc.Core.Quoting;

public static class TitleSanitizer
{
    public const int MaxLength = 60;

    private const string Removed = ";&|<>^%\"'";

    /// <summary>Makes a window title safe to use unquoted in cmd <c>title</c> and as a wt argument.</summary>
    public static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var c in value)
        {
            if (char.IsControl(c) || Removed.Contains(c))
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        var length = Math.Min(builder.Length, MaxLength);
        if (length < builder.Length && char.IsHighSurrogate(builder[length - 1]))
        {
            length--;
        }

        return builder.ToString(0, length).TrimEnd();
    }
}
