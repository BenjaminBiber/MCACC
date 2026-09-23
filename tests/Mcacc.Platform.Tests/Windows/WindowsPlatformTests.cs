using System.Runtime.Versioning;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Platform.Common;
using Mcacc.Platform.Tests.TestSupport;
using Mcacc.Platform.Windows;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.Windows;

[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public class WindowsPlatformTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [WindowsFact]
    public async Task The_wmi_snapshot_answers_within_five_seconds()
    {
        var provider = new WmiProcessSnapshotProvider();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        limit.CancelAfter(TimeSpan.FromSeconds(5));

        var processes = await provider.GetClaudeProcessesAsync(limit.Token);

        // Whether a Claude process runs is up to the machine; every row must be a plausible process, though.
        processes.ShouldAllBe(p => p.Pid > 0);
    }

    [WindowsFact]
    public void The_terminal_catalog_lists_windows_powershell_and_the_command_prompt()
    {
        var info = PlatformInfo.Current();
        var catalog = new WindowsTerminalCatalog(info, new PathExecutableLocator(info));

        var ids = catalog.GetInstalled().Select(option => option.Id).ToList();

        ids.ShouldContain(TerminalIds.WindowsPowerShell);
        ids.ShouldContain(TerminalIds.Cmd);
        catalog.GetInstalled().ShouldAllBe(option => File.Exists(option.Path));
    }

    [WindowsFact]
    public void The_default_terminal_is_one_of_the_installed_ones_or_windows_powershell()
    {
        var info = PlatformInfo.Current();
        var catalog = new WindowsTerminalCatalog(info, new PathExecutableLocator(info));

        var id = catalog.PlatformDefaultId;

        id.ShouldBeOneOf(TerminalIds.WindowsTerminal, TerminalIds.WindowsPowerShell);
    }

    [WindowsFact]
    public void The_installation_locator_returns_an_existing_path_or_nothing()
    {
        var installation = new WindowsDesktopInstallationLocator(PlatformInfo.Current()).Locate();

        if (installation is not null)
        {
            File.Exists(installation.LaunchPath).ShouldBeTrue();
        }
    }
}
