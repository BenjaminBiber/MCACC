using Mcacc.Core.Model;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Desktop;

/// <summary>Tells Claude Desktop processes apart from Claude Code CLI processes, which are also named claude(.exe).</summary>
public sealed class DesktopProcessRules
{
    private const string TypeSwitch = "--type=";
    private const string ClaudeCodeDirectory = "claude-code";

    private const string MsixDirectory = @"\WindowsApps\Claude_";
    private const string MsixExeSuffix = @"\app\Claude.exe";
    private const string SquirrelDirectory = @"\AnthropicClaude\app-";
    private const string SquirrelExeSuffix = @"\claude.exe";

    private const string MacMainExe = "/Claude.app/Contents/MacOS/Claude";
    private const string MacFrameworks = "/Claude.app/Contents/Frameworks/";
    private const string MacMainExeSuffix = "/Contents/MacOS/Claude";

    private static readonly char[] PathSeparators = ['\\', '/'];

    private readonly OsKind _os;

    private DesktopProcessRules(OsKind os) => _os = os;

    /// <remarks>Linux has no Claude Desktop, so its rules match nothing.</remarks>
    public static DesktopProcessRules ForOs(OsKind os) => new(os);

    public bool IsDesktopProcess(ProcessInfo p)
    {
        var path = p.ExePath;
        if (string.IsNullOrEmpty(path) || IsUnderClaudeCodeDirectory(path))
        {
            return false;
        }

        return _os switch
        {
            OsKind.Windows => IsWindowsDesktopExe(path),
            OsKind.MacOS => path.Contains(MacMainExe, StringComparison.Ordinal)
                            || path.Contains(MacFrameworks, StringComparison.Ordinal),
            _ => false,
        };
    }

    /// <remarks>A process whose command line is unknown is never the main process.</remarks>
    public bool IsMain(ProcessInfo p) =>
        IsDesktopProcess(p)
        && p.CommandLine is not null
        && !p.CommandLine.Contains(TypeSwitch, StringComparison.Ordinal)
        && (_os != OsKind.MacOS || p.ExePath!.EndsWith(MacMainExeSuffix, StringComparison.Ordinal));

    private static bool IsWindowsDesktopExe(string path) =>
        (path.Contains(MsixDirectory, StringComparison.OrdinalIgnoreCase)
         && path.EndsWith(MsixExeSuffix, StringComparison.OrdinalIgnoreCase))
        || (path.Contains(SquirrelDirectory, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(SquirrelExeSuffix, StringComparison.OrdinalIgnoreCase));

    private static bool IsUnderClaudeCodeDirectory(string path) =>
        path.Split(PathSeparators).Any(segment => segment.Equals(ClaudeCodeDirectory, StringComparison.OrdinalIgnoreCase));
}
