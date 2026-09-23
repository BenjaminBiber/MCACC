using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Logging;
using Mcacc.Core.Paths;

namespace Mcacc.App.Services;

/// <summary>
/// Everything the pages need, wired once in the composition root. It is a plain record, so a test can put a fake
/// into any single slot.
/// </summary>
public sealed record AppServices(
    IAccountService Accounts,
    IDesktopLauncher Desktop,
    ICodeLauncher Code,
    IUsageProvider Usage,
    ILoginInfoReader Login,
    IDesktopInstanceDetector Detector,
    DesktopInstanceMatcher Matcher,
    ITerminalCatalog Terminals,
    AccountPathResolver Resolver,
    McaccPaths Paths,
    PlatformInfo Platform,
    IShellIntegration Integration,
    IAppLifetime Lifetime,
    ILog Log,
    INavigator Navigator,
    IDialogService Dialogs,
    IShellPresenter Shell,
    string Version);

/// <summary>What a page may ask of the window: show an error, and nothing else.</summary>
public interface IShellPresenter
{
    void ShowError(string message);
}
