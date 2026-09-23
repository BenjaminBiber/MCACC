using System.Runtime.Versioning;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Platform.Common;
using Mcacc.Platform.MacOS;
using Mcacc.Platform.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.MacOS;

[Trait("Category", "Integration")]
[SupportedOSPlatform("macos")]
public class MacPlatformTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SystemProcessRunner Runner => new();

    [MacFact]
    public async Task The_ps_snapshot_lists_only_bundle_processes()
    {
        var provider = new PsProcessSnapshotProvider(Runner);

        var processes = await provider.GetClaudeProcessesAsync(Ct);

        processes.ShouldAllBe(p => p.Pid > 0);
        processes.ShouldAllBe(p => p.CommandLine!.Contains("Claude.app/Contents/"));
    }

    [MacFact]
    public void The_terminal_catalog_always_lists_terminal_app()
    {
        var catalog = new MacTerminalCatalog(PlatformInfo.Current(), Runner);

        var installed = catalog.GetInstalled();

        installed.Select(option => option.Id).ShouldContain(TerminalIds.TerminalApp);
        catalog.PlatformDefaultId.ShouldBe(TerminalIds.TerminalApp);
        installed.ShouldAllBe(option => Directory.Exists(option.Path));
    }

    [MacFact]
    public void The_installation_locator_returns_an_existing_bundle_or_nothing()
    {
        var installation = new MacDesktopInstallationLocator(PlatformInfo.Current(), Runner).Locate();

        if (installation is not null)
        {
            Directory.Exists(installation.LaunchPath).ShouldBeTrue();
            installation.LaunchPath.ShouldEndWith("Claude.app");
        }
    }

    [MacFact]
    public async Task The_keychain_source_reports_a_missing_item_as_not_logged_in()
    {
        var platform = PlatformInfo.Current();
        var paths = new McaccPaths(Path.Join(Path.GetTempPath(), "mcacc-does-not-exist"), platform.PathStyle);
        var account = new Account("mcacc-probe", "Probe", "#D97757", IsDefault: false);
        var source = new MacKeychainCredentialSource(Runner);

        // The service name is derived from a config dir no login ever used, so no prompt appears.
        var result = await source.ReadAsync(account, new AccountPathResolver(paths, platform).Resolve(account), Ct);

        result.Status.ShouldBe(CredentialReadStatus.NotLoggedIn);
    }
}
