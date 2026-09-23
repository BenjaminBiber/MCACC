using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mcacc.App.Services;
using Mcacc.App.Views;
using Mcacc.Platform;

namespace Mcacc.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = CompositionRoot.Create(PlatformServices.Create(), new DesktopAppLifetime(desktop)),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
