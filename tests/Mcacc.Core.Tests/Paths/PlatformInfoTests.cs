using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Paths;

public class PlatformInfoTests
{
    [Theory]
    [InlineData(OsKind.Windows, PathStyle.Windows)]
    [InlineData(OsKind.MacOS, PathStyle.Posix)]
    [InlineData(OsKind.Linux, PathStyle.Posix)]
    public void PathStyle_follows_the_os(OsKind os, PathStyle expected)
    {
        new PlatformInfo(os, "home", "roaming", "local").PathStyle.ShouldBe(expected);
    }

    [Fact]
    public void Current_describes_the_running_os_and_user_folders()
    {
        var info = PlatformInfo.Current();

        var expectedOs = OperatingSystem.IsWindows() ? OsKind.Windows
            : OperatingSystem.IsMacOS() ? OsKind.MacOS
            : OsKind.Linux;
        info.Os.ShouldBe(expectedOs);
        info.HomeDir.ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        info.RoamingAppData.ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        info.LocalAppData.ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }
}
