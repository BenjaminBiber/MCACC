using Mcacc.Core.Desktop;
using Mcacc.Core.Model;

namespace Mcacc.Core.Abstractions;

public interface IDesktopLauncher
{
    Task<DesktopLaunchCheck> PrepareAsync(Account target, IReadOnlyList<Account> accounts, CancellationToken ct);

    Task<DesktopLaunchOutcome> LaunchAsync(
        Account target,
        IReadOnlyList<Account> accounts,
        bool quitConflicts,
        CancellationToken ct);
}
