using Mcacc.Core.Desktop;

namespace Mcacc.Core.Abstractions;

public interface IDesktopInstallationLocator
{
    /// <returns>Null when Claude Desktop is not installed.</returns>
    DesktopInstallation? Locate();
}
