using Mcacc.Core.Desktop;
using Mcacc.Core.Model;

namespace Mcacc.Core.Abstractions;

public interface IDesktopLoginRedirector
{
    /// <summary>Watches for a login that the operating system sends to the wrong profile and hands it to
    /// <paramref name="target"/>. Returns once the login is through, Claude Desktop is gone, or the window passes.
    /// A login coming back names no account, so only the start made last can get it: a new watch ends the one of
    /// the same provider before it.</summary>
    Task<DesktopRedirectOutcome> WatchAsync(Account target, CancellationToken ct);

    /// <summary>Ends the watch of that provider, for a start that needs none itself: a login coming back now is
    /// its own.</summary>
    /// <returns>Whether a watch was running.</returns>
    bool StopWatching(ProviderKind provider);
}
