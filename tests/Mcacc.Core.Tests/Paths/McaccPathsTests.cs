using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Paths;

public class McaccPathsTests
{
    private static readonly PlatformInfo Windows =
        new(OsKind.Windows, @"C:\Users\ben", @"C:\Users\ben\AppData\Roaming", @"C:\Users\ben\AppData\Local");

    private static readonly PlatformInfo Mac = new(OsKind.MacOS, "/Users/ben", "", "");

    [Fact]
    public void Windows_layout_lives_under_the_root()
    {
        var paths = new McaccPaths(@"C:\Users\ben\.mcacc", PathStyle.Windows);

        paths.Root.ShouldBe(@"C:\Users\ben\.mcacc");
        paths.Style.ShouldBe(PathStyle.Windows);
        paths.ConfigFile.ShouldBe(@"C:\Users\ben\.mcacc\config.json");
        paths.ConfigBackupFile.ShouldBe(@"C:\Users\ben\.mcacc\config.json.bak");
        paths.UsageCacheFile.ShouldBe(@"C:\Users\ben\.mcacc\usage-cache.json");
        paths.ProfilesDir.ShouldBe(@"C:\Users\ben\.mcacc\profiles");
        paths.LaunchDir.ShouldBe(@"C:\Users\ben\.mcacc\launch");
        paths.LogsDir.ShouldBe(@"C:\Users\ben\.mcacc\logs");
        paths.ProfileDir("privat").ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat");
        paths.DesktopDataDir("privat").ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\desktop");
        paths.ClaudeConfigDir("privat").ShouldBe(@"C:\Users\ben\.mcacc\profiles\privat\claude");
    }

    [Fact]
    public void Posix_layout_lives_under_the_root()
    {
        var paths = new McaccPaths("/Users/ben/.mcacc/", PathStyle.Posix);

        paths.ConfigFile.ShouldBe("/Users/ben/.mcacc/config.json");
        paths.ConfigBackupFile.ShouldBe("/Users/ben/.mcacc/config.json.bak");
        paths.UsageCacheFile.ShouldBe("/Users/ben/.mcacc/usage-cache.json");
        paths.ProfilesDir.ShouldBe("/Users/ben/.mcacc/profiles");
        paths.LaunchDir.ShouldBe("/Users/ben/.mcacc/launch");
        paths.LogsDir.ShouldBe("/Users/ben/.mcacc/logs");
        paths.DesktopDataDir("privat").ShouldBe("/Users/ben/.mcacc/profiles/privat/desktop");
        paths.ClaudeConfigDir("privat").ShouldBe("/Users/ben/.mcacc/profiles/privat/claude");
    }

    [Fact]
    public void FromEnvironment_defaults_to_dot_mcacc_in_home()
    {
        McaccPaths.FromEnvironment(Windows, _ => null).Root.ShouldBe(@"C:\Users\ben\.mcacc");
        McaccPaths.FromEnvironment(Mac, _ => null).Root.ShouldBe("/Users/ben/.mcacc");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void FromEnvironment_ignores_an_empty_override(string value)
    {
        McaccPaths.FromEnvironment(Mac, _ => value).Root.ShouldBe("/Users/ben/.mcacc");
    }

    [Fact]
    public void FromEnvironment_uses_MCACC_HOME_when_set()
    {
        var env = new Dictionary<string, string?> { ["MCACC_HOME"] = @"D:\mcacc-test" };

        var paths = McaccPaths.FromEnvironment(Windows, name => env.GetValueOrDefault(name));

        paths.Root.ShouldBe(@"D:\mcacc-test");
        paths.Style.ShouldBe(PathStyle.Windows);
        paths.ConfigFile.ShouldBe(@"D:\mcacc-test\config.json");
    }
}
