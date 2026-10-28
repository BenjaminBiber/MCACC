using System.Text;
using Mcacc.Core.Quoting;

namespace Mcacc.Core.Terminal;

public static class PowerShellScriptBuilder
{
    public static string Build(CliLaunchRequest req)
    {
        var scrubbed = string.Join(',', req.Scrub.Select(name => "Env:" + name));
        string[] statements =
        [
            $"Remove-Item {scrubbed} -ErrorAction Ignore",
            req.ConfigDirOverride is null
                ? $"Remove-Item Env:{req.ConfigDirVariable} -ErrorAction Ignore"
                : $"$env:{req.ConfigDirVariable}={PowerShellQuoting.SingleQuote(req.ConfigDirOverride)}",
            $"Set-Location -LiteralPath {PowerShellQuoting.SingleQuote(req.HomeDir)}",
            $"$Host.UI.RawUI.WindowTitle={PowerShellQuoting.SingleQuote(req.Title)}",
            req.CliCommand,
        ];
        return string.Join("; ", statements);
    }

    /// <summary>For <c>-EncodedCommand</c>; a fallback only, because <c>-Command</c> is less likely to be flagged.</summary>
    public static string BuildEncoded(CliLaunchRequest req) =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(Build(req)));
}
