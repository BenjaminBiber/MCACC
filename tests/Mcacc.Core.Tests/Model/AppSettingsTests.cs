using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Model;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_use_platform_terminal_auto_shell_and_close_after_launch()
    {
        var settings = new AppSettings();

        settings.Terminal.ShouldBeNull();
        settings.WindowsTerminalShell.ShouldBe(WindowsTerminalShell.Auto);
        settings.CloseAfterLaunch.ShouldBeTrue();
        settings.LastAccountId.ShouldBeNull();
        settings.LastTarget.ShouldBeNull();
    }

    [Fact]
    public void Several_accounts_side_by_side_are_off_by_default()
    {
        new AppSettings().AllowParallelDesktop.ShouldBeFalse();
    }
}
