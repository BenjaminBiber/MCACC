using Mcacc.App.Localization;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Localization;

public class TrayTextsTests
{
    [Fact]
    public void An_account_line_lists_the_known_limits_and_the_running_state()
    {
        Texts.TrayAccountHeader("Privat", 42.4, 10, isRunning: true).ShouldBe("Privat · 5h 42 % · Woche 10 % · läuft");
    }

    [Fact]
    public void An_account_line_leaves_out_what_is_unknown()
    {
        Texts.TrayAccountHeader("Privat", null, 10, isRunning: false).ShouldBe("Privat · Woche 10 %");
        Texts.TrayAccountHeader("Privat", null, null, isRunning: false).ShouldBe("Privat");
    }

    [Fact]
    public void A_limit_line_names_its_reset_when_known()
    {
        var reset = DateTimeOffset.Now.AddMinutes(30);

        Texts.TrayUsageLine("5h", 42, reset).ShouldBe($"5h: 42 % · {Texts.UsageReset(reset)}");
        Texts.TrayUsageLine("5h", 42, null).ShouldBe("5h: 42 %");
    }

    [Fact]
    public void The_tooltip_shows_the_account_with_its_limits()
    {
        Texts.TrayToolTip("Privat", 42, 10).ShouldBe("MCACC – Privat · 5h 42 % · Woche 10 %");
        Texts.TrayToolTip("Privat", null, null).ShouldBe("MCACC – Privat");
        Texts.TrayToolTip(null, null, null).ShouldBe("MCACC");
    }

    [Fact]
    public void The_tooltip_is_cut_to_what_windows_shows()
    {
        var text = Texts.TrayToolTip(new string('x', 200), 42, 10);

        text.Length.ShouldBe(Texts.TrayToolTipMaxLength);
        text.ShouldEndWith("…");
    }

    [Fact]
    public void The_settings_hint_names_the_place_of_the_icon()
    {
        Texts.SettingsTrayHint(OsKind.MacOS).ShouldContain("Menüleiste");
        Texts.SettingsTrayHint(OsKind.Windows).ShouldContain("Infobereich");
    }
}
