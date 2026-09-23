using Avalonia.Headless.XUnit;
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
}
