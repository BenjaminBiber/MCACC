using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Mcacc.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Mcacc.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => Mcacc.App.Program.BuildAvaloniaApp()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
