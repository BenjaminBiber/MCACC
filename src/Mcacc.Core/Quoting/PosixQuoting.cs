namespace Mcacc.Core.Quoting;

public static class PosixQuoting
{
    public static string SingleQuote(string value) => "'" + value.Replace("'", @"'\''") + "'";
}
