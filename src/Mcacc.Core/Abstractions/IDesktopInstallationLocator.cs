using Mcacc.Core.Desktop;
using Mcacc.Core.Model;

namespace Mcacc.Core.Abstractions;

public interface IDesktopInstallationLocator
{
    /// <returns>Null when the provider's desktop app is not installed.</returns>
    DesktopInstallation? Locate(ProviderDescriptor provider);
}
