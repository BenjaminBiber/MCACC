using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Processes;

namespace Mcacc.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsShellIntegration(IProcessRunner runner) : IShellIntegration
{
    /// <exception cref="LaunchException">The explorer could not be started.</exception>
    public void RevealFolder(string path) => runner.Start(new ProcessSpec(
        "explorer.exe",
        [path],
        WorkingDirectory: null,
        ReadOnlyDictionary<string, string>.Empty,
        EnvironmentScrub.Always));

    /// <exception cref="LaunchException">No browser is registered, or it could not be started.</exception>
    public void OpenUrl(Uri url)
    {
        try
        {
            // The registered browser needs the shell, which rules out the runner's UseShellExecute=false.
            using var process = Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            throw new LaunchException($"'{url}' could not be opened.", e);
        }
    }
}
