using System.Text;
using Mcacc.Core.Processes;
using Mcacc.Core.Quoting;

namespace Mcacc.Core.Terminal;

public static class PowerShellScriptBuilder
{
    private const string ConfigDir = EnvironmentScrub.ClaudeConfigDirVariable;

    public static string Build(ClaudeCodeLaunchRequest req)
    {
        var scrubbed = string.Join(',', EnvironmentScrub.Always.Select(name => "Env:" + name));
        string[] statements =
        [
            $"Remove-Item {scrubbed} -ErrorAction Ignore",
            req.ClaudeConfigDir is null
                ? $"Remove-Item Env:{ConfigDir} -ErrorAction Ignore"
                : $"$env:{ConfigDir}={PowerShellQuoting.SingleQuote(req.ClaudeConfigDir)}",
            $"Set-Location -LiteralPath {PowerShellQuoting.SingleQuote(req.HomeDir)}",
            $"$Host.UI.RawUI.WindowTitle={PowerShellQuoting.SingleQuote(req.Title)}",
            "claude",
        ];
        return string.Join("; ", statements);
    }

    /// <summary>For <c>-EncodedCommand</c>; a fallback only, because <c>-Command</c> is less likely to be flagged.</summary>
    public static string BuildEncoded(ClaudeCodeLaunchRequest req) =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(Build(req)));
}
