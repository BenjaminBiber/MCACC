using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Desktop;

public sealed class DesktopInstanceMatcher(AccountPathResolver resolver, PathStyle style)
{
    /// <returns>The owning account, or null for a profile that belongs to no account.</returns>
    public Account? Match(DesktopInstance i, IReadOnlyList<Account> accounts)
    {
        var defaultAccount = accounts.FirstOrDefault(a => a.IsDefault);
        var userDataDir = i.UserDataDir;
        if (userDataDir is null)
        {
            return defaultAccount;
        }

        if (defaultAccount is not null
            && resolver.Resolve(defaultAccount).DesktopDataDirs.Any(dir => PathComparer.Same(dir, userDataDir, style)))
        {
            return defaultAccount;
        }

        return accounts.FirstOrDefault(a =>
            resolver.Resolve(a).DesktopUserDataDir is { } dir && PathComparer.Same(dir, userDataDir, style));
    }
}
