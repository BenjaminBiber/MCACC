using Mcacc.Core.Model;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Desktop;

/// <summary>
/// Tells the processes of a provider's desktop app from everything else that carries the same name. Both providers
/// need it, for different reasons: Claude Desktop shares its executable name with the Claude Code CLI, and the
/// Codex app shares <c>ChatGPT.exe</c> with the separate ChatGPT app. The package path decides, never the name.
/// </summary>
public sealed class DesktopProcessRules
{
    private const string TypeSwitch = "--type=";

    /// <summary>The Claude Code CLI installs under this folder and is also called claude(.exe).</summary>
    private const string ClaudeCodeDirectory = "claude-code";

    private const string SquirrelDirectory = @"\AnthropicClaude\app-";
    private const string SquirrelExeSuffix = @"\claude.exe";

    private static readonly char[] PathSeparators = ['\\', '/'];

    private readonly OsKind _os;
    private readonly ProviderDescriptor _provider;

    private DesktopProcessRules(OsKind os, ProviderDescriptor provider)
    {
        _os = os;
        _provider = provider;
    }

    /// <remarks>Linux has no desktop app, so its rules match nothing.</remarks>
    public static DesktopProcessRules For(OsKind os, ProviderDescriptor provider) => new(os, provider);

    public ProviderDescriptor Provider => _provider;

    public bool IsDesktopProcess(ProcessInfo p)
    {
        var path = p.ExePath;
        if (string.IsNullOrEmpty(path) || _provider.Desktop is not { } desktop || IsUnderClaudeCodeDirectory(path))
        {
            return false;
        }

        return _os switch
        {
            OsKind.Windows => IsWindowsDesktopExe(path, desktop),
            OsKind.MacOS => path.Contains(MacMainExe(desktop), StringComparison.Ordinal)
                            || path.Contains(MacFrameworks(desktop), StringComparison.Ordinal),
            _ => false,
        };
    }

    /// <remarks>A process whose command line is unknown is never the main process.</remarks>
    public bool IsMain(ProcessInfo p) =>
        IsDesktopProcess(p)
        && p.CommandLine is not null
        && !p.CommandLine.Contains(TypeSwitch, StringComparison.Ordinal)
        && (_os != OsKind.MacOS || p.ExePath!.EndsWith(MacMainExeSuffix(_provider.Desktop!), StringComparison.Ordinal));

    /// <summary>The packaged app, or — Claude only — a Squirrel install beside it.</summary>
    private bool IsWindowsDesktopExe(string path, ProviderDesktop desktop) =>
        (path.Contains(desktop.WindowsPathDiscriminator, StringComparison.OrdinalIgnoreCase)
         && path.EndsWith($@"\app\{desktop.WindowsProcessName}", StringComparison.OrdinalIgnoreCase))
        || (_provider.Kind == ProviderKind.Claude
            && path.Contains(SquirrelDirectory, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(SquirrelExeSuffix, StringComparison.OrdinalIgnoreCase));

    private static string MacMainExe(ProviderDesktop desktop) =>
        $"/{desktop.MacBundleName}/Contents/MacOS/{BundleStem(desktop)}";

    private static string MacFrameworks(ProviderDesktop desktop) => $"/{desktop.MacBundleName}/Contents/Frameworks/";

    private static string MacMainExeSuffix(ProviderDesktop desktop) => $"/Contents/MacOS/{BundleStem(desktop)}";

    private static string BundleStem(ProviderDesktop desktop) =>
        desktop.MacBundleName.EndsWith(".app", StringComparison.Ordinal)
            ? desktop.MacBundleName[..^4]
            : desktop.MacBundleName;

    /// <summary>Only Claude needs this: the CLI there carries the same executable name as the app.</summary>
    private bool IsUnderClaudeCodeDirectory(string path) =>
        _provider.Kind == ProviderKind.Claude
        && path.Split(PathSeparators).Any(segment => segment.Equals(ClaudeCodeDirectory, StringComparison.OrdinalIgnoreCase));
}
