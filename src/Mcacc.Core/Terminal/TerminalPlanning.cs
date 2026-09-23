using System.Collections.ObjectModel;
using Mcacc.Core.Accounts;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Terminal;

internal static class TerminalPlanning
{
    // The scripts set CLAUDE_CONFIG_DIR themselves, so nothing is inherited.
    private static readonly IReadOnlyList<string> EnvUnset =
        [.. EnvironmentScrub.Always, EnvironmentScrub.ClaudeConfigDirVariable];

    public static ProcessSpec Process(string fileName, IReadOnlyList<string> arguments, string? workingDirectory) =>
        new(fileName, arguments, workingDirectory, ReadOnlyDictionary<string, string>.Empty, EnvUnset);

    /// <summary>The id names the launch file and appears unquoted in <c>cmd /k &lt;id&gt;.cmd</c>.</summary>
    public static void RequireSlugAccountId(ClaudeCodeLaunchRequest req)
    {
        if (!AccountId.IsValid(req.AccountId))
        {
            throw new ArgumentException($"Account id '{req.AccountId}' is not a slug.", nameof(req));
        }
    }
}
