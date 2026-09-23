using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Paths;

public class AccountPathResolverTests
{
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private static readonly PlatformInfo Windows =
        new(OsKind.Windows, @"C:\Users\ben", @"C:\Users\ben\AppData\Roaming", @"C:\Users\ben\AppData\Local");

    private static readonly PlatformInfo Mac = new(OsKind.MacOS, "/Users/ben", "", "");

    private static AccountPathResolver ResolverFor(PlatformInfo info) =>
        new(McaccPaths.FromEnvironment(info, _ => null), info);

    [Fact]
    public void Windows_default_account_uses_the_real_claude_locations()
    {
        var paths = ResolverFor(Windows).Resolve(Account.CreateDefault());

        paths.DesktopUserDataDir.ShouldBeNull();
        paths.ClaudeConfigDir.ShouldBeNull();
        paths.EffectiveClaudeDir.ShouldBe(@"C:\Users\ben\.claude");
        paths.ClaudeJsonFile.ShouldBe(@"C:\Users\ben\.claude.json");
        paths.CredentialsFile.ShouldBe(@"C:\Users\ben\.claude\.credentials.json");
        paths.DesktopDataDirs.ShouldBe([
            @"C:\Users\ben\AppData\Local\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude",
            @"C:\Users\ben\AppData\Roaming\Claude",
        ]);
    }

    [Fact]
    public void Windows_profile_account_lives_under_the_mcacc_profile()
    {
        var paths = ResolverFor(Windows).Resolve(Privat);

        paths.DesktopUserDataDir.ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\desktop");
        paths.ClaudeConfigDir.ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\claude");
        paths.EffectiveClaudeDir.ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\claude");
        paths.ClaudeJsonFile.ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\claude\.claude.json");
        paths.CredentialsFile.ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\claude\.credentials.json");
        paths.DesktopDataDirs.ShouldBe([@"C:\Users\ben\.mcacc\profiles\privat\desktop"]);
    }

    [Fact]
    public void Mac_default_account_uses_the_real_claude_locations()
    {
        var paths = ResolverFor(Mac).Resolve(Account.CreateDefault());

        paths.DesktopUserDataDir.ShouldBeNull();
        paths.ClaudeConfigDir.ShouldBeNull();
        paths.EffectiveClaudeDir.ShouldBe("/Users/ben/.claude");
        paths.ClaudeJsonFile.ShouldBe("/Users/ben/.claude.json");
        paths.CredentialsFile.ShouldBe("/Users/ben/.claude/.credentials.json");
        paths.DesktopDataDirs.ShouldBe(["/Users/ben/Library/Application Support/Claude"]);
    }

    [Fact]
    public void Mac_profile_account_lives_under_the_mcacc_profile()
    {
        var paths = ResolverFor(Mac).Resolve(Privat);

        paths.DesktopUserDataDir.ShouldBe("/Users/ben/.mcacc/profiles/privat/desktop");
        paths.ClaudeConfigDir.ShouldBe("/Users/ben/.mcacc/profiles/privat/claude");
        paths.EffectiveClaudeDir.ShouldBe("/Users/ben/.mcacc/profiles/privat/claude");
        paths.ClaudeJsonFile.ShouldBe("/Users/ben/.mcacc/profiles/privat/claude/.claude.json");
        paths.CredentialsFile.ShouldBe("/Users/ben/.mcacc/profiles/privat/claude/.credentials.json");
        paths.DesktopDataDirs.ShouldBe(["/Users/ben/.mcacc/profiles/privat/desktop"]);
    }

    [Fact]
    public void Default_is_decided_by_the_flag_not_the_id()
    {
        var renamedDefault = new Account("work", "Firma", "#D97757", IsDefault: true);

        var paths = ResolverFor(Mac).Resolve(renamedDefault);

        paths.ClaudeConfigDir.ShouldBeNull();
        paths.EffectiveClaudeDir.ShouldBe("/Users/ben/.claude");
    }

    [Fact]
    public void Linux_default_account_has_no_known_desktop_data_dir()
    {
        var linux = new PlatformInfo(OsKind.Linux, "/home/ben", "", "");

        ResolverFor(linux).Resolve(Account.CreateDefault()).DesktopDataDirs.ShouldBeEmpty();
    }
}
