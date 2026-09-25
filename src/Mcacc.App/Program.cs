using Avalonia;
using Mcacc.App.Theme;
using Mcacc.Core.Config;
using Mcacc.Core.Paths;
using Mcacc.Platform;
using Mcacc.Platform.Common;

namespace Mcacc.App;

internal static class Program
{
    private const string DiagnoseSwitch = "--diagnose";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains(DiagnoseSwitch, StringComparer.OrdinalIgnoreCase))
        {
            return Diagnose();
        }

        using var single = SingleInstance.Acquire();
        if (!single.IsOnlyInstance)
        {
            if (StartupArgs.ShouldSignalExisting(args))
            {
                SingleInstance.SignalExisting();
            }

            return 0;
        }

        return BuildAvaloniaApp().With(MacOptions(args)).StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Avalonia.Native applies <see cref="MacOSPlatformOptions.ShowInDock"/> when the app finishes launching, which is
    /// after <see cref="App.OnFrameworkInitializationCompleted"/> already ran. A start at login has to launch without
    /// the Dock icon from the start, or Avalonia switches back to Regular and steals focus.
    /// </summary>
    internal static MacOSPlatformOptions MacOptions(string[] args) => new() { ShowInDock = !StartupArgs.IsHidden(args) };

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .ConfigureFonts(fonts => fonts.AddFontCollection(new McaccFontCollection()))
        .LogToTrace();

    /// <summary>Writes the report to <c>logs/diagnose.txt</c>, prints the path and exits without any UI.</summary>
    private static int Diagnose()
    {
        ParentConsole.Attach();
        try
        {
            var services = PlatformServices.Create();
            var accounts = new ConfigStore(services.Paths).Load().Config.Accounts;
            var report = DiagnosticsReport.BuildAsync(services, accounts, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var file = services.Paths.Style.Join(services.Paths.LogsDir, "diagnose.txt");
            Directory.CreateDirectory(services.Paths.LogsDir);
            File.WriteAllText(file, report);
            Console.WriteLine(file);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"{e.GetType().Name}: {e.Message}");
            return 1;
        }
    }
}
