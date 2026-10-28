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
        paths.ConfigDirOverride.ShouldBeNull();
        paths.EffectiveConfigDir.ShouldBe(@"C:\Users\ben\.claude");
        paths.IdentityFile.ShouldBe(@"C:\Users\ben\.claude.json");
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
        paths.ConfigDirOverride.ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\claude");
        paths.EffectiveConfigDir.ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\claude");
        paths.IdentityFile.ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\claude\.claude.json");
        paths.CredentialsFile.ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\claude\.credentials.json");
        paths.DesktopDataDirs.ShouldBe([@"C:\Users\ben\.mcacc\profiles\privat\desktop"]);
    }

    [Fact]
    public void Mac_default_account_uses_the_real_claude_locations()
    {
        var paths = ResolverFor(Mac).Resolve(Account.CreateDefault());

        paths.DesktopUserDataDir.ShouldBeNull();
        paths.ConfigDirOverride.ShouldBeNull();
        paths.EffectiveConfigDir.ShouldBe("/Users/ben/.claude");
        paths.IdentityFile.ShouldBe("/Users/ben/.claude.json");
        paths.CredentialsFile.ShouldBe("/Users/ben/.claude/.credentials.json");
        paths.DesktopDataDirs.ShouldBe(["/Users/ben/Library/Application Support/Claude"]);
    }

    [Fact]
    public void Mac_profile_account_lives_under_the_mcacc_profile()
    {
        var paths = ResolverFor(Mac).Resolve(Privat);

        paths.DesktopUserDataDir.ShouldBe("/Users/ben/.mcacc/profiles/privat/desktop");
        paths.ConfigDirOverride.ShouldBe("/Users/ben/.mcacc/profiles/privat/claude");
        paths.EffectiveConfigDir.ShouldBe("/Users/ben/.mcacc/profiles/privat/claude");
        paths.IdentityFile.ShouldBe("/Users/ben/.mcacc/profiles/privat/claude/.claude.json");
        paths.CredentialsFile.ShouldBe("/Users/ben/.mcacc/profiles/privat/claude/.credentials.json");
        paths.DesktopDataDirs.ShouldBe(["/Users/ben/.mcacc/profiles/privat/desktop"]);
    }

    [Fact]
    public void Default_is_decided_by_the_flag_not_the_id()
    {
        var renamedDefault = new Account("work", "Firma", "#D97757", IsDefault: true);

        var paths = ResolverFor(Mac).Resolve(renamedDefault);

        paths.ConfigDirOverride.ShouldBeNull();
        paths.EffectiveConfigDir.ShouldBe("/Users/ben/.claude");
    }

    [Fact]
    public void Linux_default_account_has_no_known_desktop_data_dir()
    {
        var linux = new PlatformInfo(OsKind.Linux, "/home/ben", "", "");

        ResolverFor(linux).Resolve(Account.CreateDefault()).DesktopDataDirs.ShouldBeEmpty();
    }

    private static readonly Account CodexPrivat =
        new("codex-privat", "Codex", "#5E8C8A", IsDefault: false, ProviderKind.Codex);

    [Fact]
    public void Windows_Codex_default_account_uses_the_real_codex_locations()
    {
        var paths = ResolverFor(Windows).Resolve(Account.CreateDefault(ProviderKind.Codex));

        paths.Provider.ShouldBeSameAs(ProviderDescriptors.Codex);
        paths.DesktopUserDataDir.ShouldBeNull();
        paths.ConfigDirOverride.ShouldBeNull();
        paths.EffectiveConfigDir.ShouldBe(@"C:\Users\ben\.codex");
        paths.IdentityFile.ShouldBe(@"C:\Users\ben\.codex\auth.json");
        paths.DesktopDataDirs.ShouldBe([
            @"C:\Users\ben\AppData\Local\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Roaming\Codex",
            @"C:\Users\ben\AppData\Roaming\Codex",
        ]);
    }

    [Fact]
    public void Windows_Codex_profile_account_lives_under_the_mcacc_profile()
    {
        var paths = ResolverFor(Windows).Resolve(CodexPrivat);

        paths.DesktopUserDataDir.ShouldBe(@"C:\Users\ben\.mcacc\profiles\codex-privat\codex-desktop");
        paths.ConfigDirOverride.ShouldBe(@"C:\Users\ben\.mcacc\profiles\codex-privat\codex");
        paths.EffectiveConfigDir.ShouldBe(@"C:\Users\ben\.mcacc\profiles\codex-privat\codex");
        paths.IdentityFile.ShouldBe(@"C:\Users\ben\.mcacc\profiles\codex-privat\codex\auth.json");
        paths.DesktopDataDirs.ShouldBe([@"C:\Users\ben\.mcacc\profiles\codex-privat\codex-desktop"]);
    }

    [Fact]
    public void Mac_Codex_default_account_uses_the_real_codex_locations()
    {
        var paths = ResolverFor(Mac).Resolve(Account.CreateDefault(ProviderKind.Codex));

        paths.EffectiveConfigDir.ShouldBe("/Users/ben/.codex");
        paths.IdentityFile.ShouldBe("/Users/ben/.codex/auth.json");
        paths.DesktopDataDirs.ShouldBe(["/Users/ben/Library/Application Support/Codex"]);
    }

    [Fact]
    public void Mac_Codex_profile_account_lives_under_the_mcacc_profile()
    {
        var paths = ResolverFor(Mac).Resolve(CodexPrivat);

        paths.DesktopUserDataDir.ShouldBe("/Users/ben/.mcacc/profiles/codex-privat/codex-desktop");
        paths.ConfigDirOverride.ShouldBe("/Users/ben/.mcacc/profiles/codex-privat/codex");
        paths.IdentityFile.ShouldBe("/Users/ben/.mcacc/profiles/codex-privat/codex/auth.json");
    }

    /// <summary>Codex keeps identity and token in one file; nothing downstream may assume two.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_Codex_account_has_one_file_for_identity_and_token(bool isDefault)
    {
        var account = isDefault ? Account.CreateDefault(ProviderKind.Codex) : CodexPrivat;

        var paths = ResolverFor(Windows).Resolve(account);

        paths.CredentialsFile.ShouldBe(paths.IdentityFile);
    }

    /// <summary>Claude keeps them apart, which is why the wrong-account check exists for Claude only.</summary>
    [Fact]
    public void A_Claude_account_has_two_different_files()
    {
        var paths = ResolverFor(Windows).Resolve(Privat);

        paths.CredentialsFile.ShouldNotBe(paths.IdentityFile);
    }

    /// <summary>The guard for the migration: an existing Claude profile must keep the folders it already has.</summary>
    [Fact]
    public void A_Claude_profile_keeps_the_subfolders_it_always_had()
    {
        var paths = ResolverFor(Windows).Resolve(Privat);

        paths.ConfigDirOverride.ShouldEndWith(@"\privat\claude");
        paths.DesktopUserDataDir.ShouldEndWith(@"\privat\desktop");
    }

    [Fact]
    public void An_unknown_provider_is_rejected_instead_of_resolved_as_Claude()
    {
        var strange = Privat with { Provider = (ProviderKind)42 };

        Should.Throw<ArgumentOutOfRangeException>(() => ResolverFor(Windows).Resolve(strange));
    }
}
