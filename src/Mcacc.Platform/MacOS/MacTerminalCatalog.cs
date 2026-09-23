using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Terminal;

namespace Mcacc.Platform.MacOS;

[SupportedOSPlatform("macos")]
public sealed class MacTerminalCatalog(PlatformInfo info, IProcessRunner runner) : ITerminalCatalog
{
    private const string TerminalAppPath = "/System/Applications/Utilities/Terminal.app";

    private IReadOnlyList<TerminalOption>? _installed;

    public string PlatformDefaultId => TerminalIds.TerminalApp;

    /// <remarks>Scanned once; an install or uninstall during the session is not picked up.</remarks>
    public IReadOnlyList<TerminalOption> GetInstalled() => _installed ??= Scan();

    private IReadOnlyList<TerminalOption> Scan()
    {
        // Terminal.app ships with macOS, so its id is listed even if the bundle moved.
        var options = new List<TerminalOption> { new(TerminalIds.TerminalApp, "Terminal", TerminalAppPath) };
        Add(options, TerminalIds.ITerm2, "iTerm2", "iTerm.app", "com.googlecode.iterm2");
        Add(options, TerminalIds.Ghostty, "Ghostty", "Ghostty.app", "com.mitchellh.ghostty");
        return options;
    }

    private void Add(List<TerminalOption> options, string id, string displayName, string bundleName, string bundleId)
    {
        if (MacBundleLocator.Find(info, runner, bundleName, bundleId) is { } path)
        {
            options.Add(new TerminalOption(id, displayName, path));
        }
    }
}
