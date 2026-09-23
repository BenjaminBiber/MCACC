using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Mcacc.Core.ClaudeCode;

public static class KeychainServiceName
{
    private const string Base = "Claude Code-credentials";

    private const int SuffixLength = 8;

    /// <summary>The macOS keychain service Claude Code stores its token under.</summary>
    /// <param name="claudeConfigDir">Null for the default account, which uses no <c>CLAUDE_CONFIG_DIR</c>.</param>
    // Scheme to be confirmed by spike M7.
    public static string For(string? claudeConfigDir)
    {
        if (claudeConfigDir is null)
        {
            return Base;
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(claudeConfigDir));
        var hex = Convert.ToHexString(digest).ToLower(CultureInfo.InvariantCulture);
        return $"{Base}-{hex[..SuffixLength]}";
    }
}
