using Mcacc.App.Services;
using Mcacc.App.Tests.TestSupport;
using Mcacc.Core.Logging;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Services;

public class LoginItemSyncTests
{
    private readonly FakeLoginItem _item = new();

    private LoginItemSync Sync(bool isActive = true, string? path = @"C:\Tools\MCACC.exe") =>
        new(_item, path, isActive, NullLog.Instance);

    [Fact]
    public void On_points_the_entry_at_the_running_exe()
    {
        Sync().Apply(launchAtLogin: true);

        _item.Enabled.ShouldBe(@"C:\Tools\MCACC.exe");
    }

    [Fact]
    public void Off_removes_the_entry()
    {
        var sync = Sync();
        sync.Apply(launchAtLogin: true);

        sync.Apply(launchAtLogin: false);

        _item.Enabled.ShouldBeNull();
        _item.Calls.ShouldBe(["enable", "disable"]);
    }

    [Fact]
    public void An_inactive_sync_leaves_the_entry_alone()
    {
        var sync = Sync(isActive: false);

        sync.Apply(launchAtLogin: true);
        sync.Apply(launchAtLogin: false);

        _item.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Without_a_path_nothing_is_enabled()
    {
        Sync(path: null).Apply(launchAtLogin: true);

        _item.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void A_failing_entry_does_not_throw()
    {
        _item.ThrowOnApply = new UnauthorizedAccessException("denied");

        Should.NotThrow(() => Sync().Apply(launchAtLogin: true));
    }

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(false, "", true)]
    [InlineData(true, null, false)]
    [InlineData(false, @"C:\temp\mcacc-test", false)]
    public void Only_a_release_build_with_the_real_data_folder_keeps_the_entry(bool debug, string? home, bool expected)
    {
        LoginItemSync.IsActive(debug, home).ShouldBe(expected);
    }
}
