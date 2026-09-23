namespace Mcacc.Core.Quoting;

public static class WtEscaping
{
    /// <summary>wt.exe splits its command line into subcommands at every unescaped <c>;</c>.</summary>
    public static string EscapeSemicolons(string value) => value.Replace(";", @"\;");
}
