using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Platform.MacOS;

[SupportedOSPlatform("macos")]
public sealed class MacDesktopInstallationLocator(PlatformInfo info, IProcessRunner runner) : IDesktopInstallationLocator
{
    private readonly ConcurrentDictionary<ProviderKind, DesktopInstallation?> _found = new();

    /// <remarks>Searched once per provider; installing the app while MCACC runs is not picked up.</remarks>
    public DesktopInstallation? Locate(ProviderDescriptor provider) => _found.GetOrAdd(provider.Kind, _ =>
    {
        if (provider.Desktop is not { } desktop)
        {
            return null;
        }

        var bundle = MacBundleLocator.Find(info, runner, desktop.MacBundleName, desktop.MacBundleId);
        return bundle is null ? null : new DesktopInstallation(DesktopInstallKind.MacBundle, bundle);
    });
}
