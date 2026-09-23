using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Processes;

namespace Mcacc.Platform.MacOS;

[SupportedOSPlatform("macos")]
public sealed class MacShellIntegration(IProcessRunner runner) : IShellIntegration
{
    private const string Open = "/usr/bin/open";

    /// <exception cref="LaunchException">The Finder could not be opened.</exception>
    public void RevealFolder(string path) => Start(path);

    /// <exception cref="LaunchException">No browser is registered, or it could not be started.</exception>
    public void OpenUrl(Uri url) => Start(url.AbsoluteUri);

    private void Start(string argument) => runner.Start(new ProcessSpec(
        Open,
        [argument],
        WorkingDirectory: null,
        ReadOnlyDictionary<string, string>.Empty,
        EnvironmentScrub.Always));
}
