namespace Mcacc.Core.Quoting;

public static class BatchEscaping
{
    /// <summary>Escapes a value for use between the quotes of <c>set "NAME=value"</c> or <c>cd /d "value"</c> in a batch file.</summary>
    /// <exception cref="ArgumentException">The value contains a double quote or a line break, which cannot be escaped there.</exception>
    public static string ForSetValue(string value)
    {
        if (value.AsSpan().IndexOfAny('"', '\r', '\n') >= 0)
        {
            throw new ArgumentException("A batch value must not contain double quotes or line breaks.", nameof(value));
        }

        return value.Replace("%", "%%");
    }
}
