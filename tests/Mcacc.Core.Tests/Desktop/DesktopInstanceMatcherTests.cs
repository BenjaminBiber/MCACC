using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Desktop;

public class DesktopInstanceMatcherTests
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);
    private static readonly Account Arbeit = new("arbeit", "Arbeit", "#788C5D", IsDefault: false);
    private static readonly IReadOnlyList<Account> Accounts = [Privat, Standard, Arbeit];

    private static readonly PlatformInfo Windows =
        new(OsKind.Windows, @"C:\Users\USER", @"C:\Users\USER\AppData\Roaming", @"C:\Users\USER\AppData\Local");

    private static readonly PlatformInfo Mac = new(OsKind.MacOS, "/Users/USER", "", "");

    private static DesktopInstanceMatcher MatcherFor(PlatformInfo info) =>
        new(new AccountPathResolver(McaccPaths.FromEnvironment(info, _ => null), info), info.PathStyle);

    private static DesktopInstance Instance(string? userDataDir) => new(100, StartTime: null, userDataDir, [100]);

    [Theory]
    [InlineData(OsKind.Windows)]
    [InlineData(OsKind.MacOS)]
    public void Instance_without_a_flag_belongs_to_the_default_account(OsKind os)
    {
        MatcherFor(os == OsKind.Windows ? Windows : Mac).Match(Instance(null), Accounts).ShouldBe(Standard);
    }

    [Fact]
    public void Default_account_is_found_by_its_flag_not_its_id()
    {
        var renamedDefault = new Account("firma", "Firma", "#D97757", IsDefault: true);

        MatcherFor(Windows).Match(Instance(null), [Privat, renamedDefault]).ShouldBe(renamedDefault);
    }

    [Theory]
    [InlineData(@"C:\Users\USER\AppData\Roaming\Claude")]
    [InlineData(@"C:\Users\USER\AppData\Local\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude")]
    [InlineData("c:/users/user/appdata/roaming/claude/")]
    public void Windows_default_data_dirs_belong_to_the_default_account(string userDataDir)
    {
        MatcherFor(Windows).Match(Instance(userDataDir), Accounts).ShouldBe(Standard);
    }

    [Theory]
    [InlineData(@"C:\Users\USER\.mcacc\profiles\privat\desktop", "privat")]
    [InlineData(@"c:\users\user\.mcacc\profiles\PRIVAT\desktop\", "privat")]
    [InlineData(@"C:\Users\USER\.mcacc\profiles\arbeit\desktop", "arbeit")]
    public void Windows_profile_dir_belongs_to_its_account(string userDataDir, string expectedId)
    {
        MatcherFor(Windows).Match(Instance(userDataDir), Accounts).ShouldNotBeNull().Id.ShouldBe(expectedId);
    }

    [Theory]
    [InlineData(@"C:\Users\USER\desk top ä")]
    [InlineData(@"C:\Users\USER\.mcacc\profiles\geloescht\desktop")]
    [InlineData(@"C:\Users\USER\.mcacc\profiles\privat")]
    [InlineData(@"C:\Users\USER\AppData\Roaming")]
    public void Windows_unknown_dir_belongs_to_no_account(string userDataDir)
    {
        MatcherFor(Windows).Match(Instance(userDataDir), Accounts).ShouldBeNull();
    }

    [Theory]
    [InlineData("/Users/USER/Library/Application Support/Claude", "standard")]
    [InlineData("/Users/USER/.mcacc/profiles/privat/desktop", "privat")]
    [InlineData("/Users/USER/.mcacc/profiles/arbeit/desktop/", "arbeit")]
    public void Mac_dirs_belong_to_their_account(string userDataDir, string expectedId)
    {
        MatcherFor(Mac).Match(Instance(userDataDir), Accounts).ShouldNotBeNull().Id.ShouldBe(expectedId);
    }

    [Fact]
    public void Mac_unknown_dir_belongs_to_no_account()
    {
        MatcherFor(Mac).Match(Instance("/Users/USER/desk top ä"), Accounts).ShouldBeNull();
    }

    [Fact]
    public void Default_windows_snapshot_belongs_to_the_default_account()
    {
        var instance = DesktopProcessClassifier
            .Classify(WmiCsvFixture.Load("wmi-default.csv"), DesktopProcessRules.For(OsKind.Windows, ProviderDescriptors.Claude))
            .ShouldHaveSingleItem();

        MatcherFor(Windows).Match(instance, Accounts).ShouldBe(Standard);
    }

    [Fact]
    public void Mac_profile_snapshot_has_the_profile_account_and_an_unknown_profile()
    {
        var instances = DesktopProcessClassifier.Classify(
            PsOutputParser.Parse(FixtureFiles.ReadAllText("ps-profile.txt")),
            DesktopProcessRules.For(OsKind.MacOS, ProviderDescriptors.Claude));

        var matcher = MatcherFor(Mac);
        instances.Select(i => matcher.Match(i, Accounts)).ShouldBe([Privat, null]);
    }
}
