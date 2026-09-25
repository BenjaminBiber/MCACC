using Shouldly;
using Xunit;

namespace Mcacc.App.Tests;

public class StartupArgsTests
{
    [Fact]
    public void A_plain_start_shows_the_window()
    {
        StartupArgs.IsHidden([]).ShouldBeFalse();
        StartupArgs.IsHidden(null).ShouldBeFalse();
    }

    [Theory]
    [InlineData("--hidden")]
    [InlineData("--HIDDEN")]
    public void A_start_at_login_stays_in_the_tray(string arg)
    {
        StartupArgs.IsHidden(["--foo", arg]).ShouldBeTrue();
    }

    [Fact]
    public void A_second_plain_start_brings_the_running_window_up()
    {
        StartupArgs.ShouldSignalExisting([]).ShouldBeTrue();
    }

    [Fact]
    public void A_second_start_at_login_leaves_the_running_mcacc_alone()
    {
        StartupArgs.ShouldSignalExisting(["--hidden"]).ShouldBeFalse();
    }
}
