using Avalonia.Headless.XUnit;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.Tray;
using Mcacc.App.Views;
using Shouldly;

namespace Mcacc.App.Tests;

public class MainWindowTests
{
    [AvaloniaFact]
    public void MainWindow_Title_Is_MCACC()
    {
        var window = new MainWindow();

        window.Title.ShouldBe("MCACC");
    }

    [AvaloniaFact]
    public void Closing_the_window_only_hides_it()
    {
        var lifetime = new FakeAppLifetime();
        var window = new MainWindow { Lifetime = lifetime };
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();

        window.Close();

        closed.ShouldBeFalse();
        lifetime.Hides.ShouldBe(1);
        lifetime.Shutdowns.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Without_a_lifetime_the_window_closes_as_usual()
    {
        var window = new MainWindow();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();

        window.Close();

        closed.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_menu_bar_icon_is_drawn_from_the_icon_font()
    {
        using var bitmap = TrayIconImage.RenderTemplate();

        bitmap.PixelSize.Width.ShouldBe(TrayIconImage.TemplateSize);
        bitmap.PixelSize.Height.ShouldBe(TrayIconImage.TemplateSize);
    }

    [AvaloniaFact]
    public void The_notification_area_icon_loads()
    {
        TrayIconImage.Windows().ShouldNotBeNull();
    }
}
