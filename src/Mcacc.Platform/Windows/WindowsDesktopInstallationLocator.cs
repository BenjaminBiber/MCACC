using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Paths;

namespace Mcacc.Platform.Windows;

/// <summary>
/// Prefers the MSIX invocation alias, which passes arguments through to the packaged app, and falls back to a
/// Squirrel install.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDesktopInstallationLocator(PlatformInfo info) : IDesktopInstallationLocator
{
    public DesktopInstallation? Locate()
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
}
