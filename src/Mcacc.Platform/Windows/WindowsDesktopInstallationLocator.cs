using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;

namespace Mcacc.Platform.Windows;

/// <summary>
/// Finds the desktop app of a provider. The two packages are reachable in different ways, which is Windows
/// knowledge and stays here rather than in the cross-platform descriptor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDesktopInstallationLocator(PlatformInfo info, IProcessRunner runner)
    : IDesktopInstallationLocator
{
    private const string PowerShell = "powershell.exe";
    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(10);

    public DesktopInstallation? Locate(ProviderDescriptor provider) => provider.Kind switch
    {
        ProviderKind.Claude => Claude(),
        ProviderKind.Codex => Codex(),
        _ => null,
    };

    /// <summary>
    /// Prefers the MSIX invocation alias, which passes arguments through to the packaged app, and falls back to a
    /// Squirrel install.
    /// </summary>
    private DesktopInstallation? Claude()
    {
        var style = info.PathStyle;
        var alias = style.Join(info.LocalAppData, "Microsoft", "WindowsApps", "claude-desktop.exe");
        if (File.Exists(alias))
        {
            return new DesktopInstallation(DesktopInstallKind.Msix, alias);
        }

        var squirrel = style.Join(info.LocalAppData, "AnthropicClaude", "claude.exe");
        return File.Exists(squirrel) ? new DesktopInstallation(DesktopInstallKind.Squirrel, squirrel) : null;
    }

    /// <summary>
    /// The Codex package declares no invocation alias for its main app, and <c>WindowsApps</c> cannot be listed by
    /// a normal user, so the install location has to be asked for. It carries the version and changes with every
    /// update, which is why it is never remembered.
    /// </summary>
    private DesktopInstallation? Codex()
    {
        if (InstallLocation("OpenAI.Codex") is not { } location)
        {
            return null;
        }

        var exe = info.PathStyle.Join(location, "app", "ChatGPT.exe");
        return File.Exists(exe) ? new DesktopInstallation(DesktopInstallKind.Msix, exe) : null;
    }

    private string? InstallLocation(string packageName)
    {
        var spec = new ProcessSpec(
            PowerShell,
            [
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                $"(Get-AppxPackage -Name '{packageName}').InstallLocation",
            ],
            WorkingDirectory: null,
            new Dictionary<string, string>(),
            []);

        try
        {
            var result = Task.Run(() => runner.RunAsync(spec, LookupTimeout, CancellationToken.None))
                .GetAwaiter()
                .GetResult();
            var location = result.StdOut.Trim();
            return location.Length > 0 && Directory.Exists(location) ? location : null;
        }
        catch (Exception e) when (e is LaunchException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
