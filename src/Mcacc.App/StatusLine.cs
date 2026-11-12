using System.Text;
using Mcacc.Core.ClaudeCode;

namespace Mcacc.App;

/// <summary>
/// MCACC started by Claude Code as its status line: no window, no single-instance check. It takes the limits out of
/// the json on stdin and writes them to the config folder of the account that runs, which Claude Code names in
/// <c>CLAUDE_CONFIG_DIR</c> (unset for the account in <c>~/.claude</c>). It prints nothing, so the status line stays
/// empty, and it always ends with 0: a status line must never get in the way of a session.
/// </summary>
internal static class StatusLine
{
    private const int MaxInput = 1 << 20;

    public static int Run()
    {
        try
        {
            using var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var buffer = new char[MaxInput];
            var length = stdin.ReadBlock(buffer, 0, buffer.Length);
            ClaudeLimitsFile.Record(ConfigDir(Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")), new string(buffer, 0, length), DateTimeOffset.UtcNow);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The next reply of the session writes again.
        }

        return 0;
    }

    internal static string ConfigDir(string? claudeConfigDir) => string.IsNullOrWhiteSpace(claudeConfigDir)
        ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
        : claudeConfigDir;
}
