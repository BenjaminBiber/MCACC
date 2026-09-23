using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Paths;

namespace Mcacc.Platform.MacOS;

[SupportedOSPlatform("macos")]
public sealed class MacDesktopInstallationLocator(PlatformInfo info, IProcessRunner runner) : IDesktopInstallationLocator
{
    private const string BundleName = "Claude.app";

    private DesktopInstallation? _installation;
    private bool _searched;

    /// <remarks>Searched once; installing Claude Desktop while MCACC runs is not picked up.</remarks>
    public DesktopInstallation? Locate()
    {
        if (_searched)
        {
            return _installation;
        }

        _searched = true;
        var bundle = MacBundleLocator.Find(info, runner, BundleName, ClaudeDesktopConstants.MacBundleId);
        _installation = bundle is null ? null : new DesktopInstallation(DesktopInstallKind.MacBundle, bundle);
        return _installation;
    }
}
