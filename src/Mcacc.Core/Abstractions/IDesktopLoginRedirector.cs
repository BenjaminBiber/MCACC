using Mcacc.Core.Desktop;
using Mcacc.Core.Model;

namespace Mcacc.Core.Abstractions;

public interface IDesktopLoginRedirector
{
    /// <summary>Watches for a login that the operating system sends to the wrong profile and hands it to
    /// <paramref name="target"/>. Returns once the login is through, Claude Desktop is gone, or the window passes.</summary>
    Task<DesktopRedirectOutcome> WatchAsync(Account target, CancellationToken ct);
}
