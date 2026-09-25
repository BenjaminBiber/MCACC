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

    [Fact]
    public void An_exe_started_from_inside_a_temp_dir_is_transient()
    {
        LoginItemSync.IsTransient(@"C:\Temp\Temp1_MCACC.zip\MCACC.exe", [@"C:\Temp"]).ShouldBeTrue();
    }

    [Fact]
    public void An_app_under_mac_os_app_translocation_is_transient()
    {
        LoginItemSync.IsTransient(
            "/private/var/folders/x/AppTranslocation/y/MCACC.app/Contents/MacOS/MCACC",
            []).ShouldBeTrue();
    }

    [Fact]
    public void An_exe_in_a_normal_install_folder_is_not_transient()
    {
        LoginItemSync.IsTransient(@"C:\Tools\MCACC.exe", [@"C:\Temp"]).ShouldBeFalse();
    }

    [Fact]
    public void An_exe_under_a_folder_that_only_shares_the_temp_dir_prefix_is_not_transient()
    {
        LoginItemSync.IsTransient(@"C:\TempTools\MCACC.exe", [@"C:\Temp"]).ShouldBeFalse();
    }

    [Fact]
    public void A_trailing_separator_on_the_temp_dir_still_matches()
    {
        LoginItemSync.IsTransient(@"C:\Temp\x\MCACC.exe", [@"C:\Temp\"]).ShouldBeTrue();
    }

    [Fact]
    public void A_start_from_a_transient_path_leaves_the_login_entry_as_it_is()
    {
        var log = new FakeLog();
        var sync = LoginItemSync.ForProcess(_item, log, @"C:\Temp\Temp1_MCACC.zip\MCACC.exe", [@"C:\Temp"]);

        sync.Apply(launchAtLogin: true);

        _item.Calls.ShouldBeEmpty();
        log.InfoMessages.ShouldHaveSingleItem();
    }
}
