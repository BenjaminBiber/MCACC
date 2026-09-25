using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mcacc.App.Services;
using Mcacc.App.Tray;
using Mcacc.App.ViewModels;
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
            Start(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// MCACC lives in the tray: the window comes and goes, and only the tray's quit or Cmd+Q ends the process. A start
    /// at login (<see cref="StartupArgs.IsHidden"/>) leaves the window hidden until the tray or a second start asks for it.
    /// </summary>
    private void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var platform = PlatformServices.Create();
        var lifetime = new DesktopAppLifetime(desktop, platform.Dock);
        var (shell, services) = CompositionRoot.Create(platform, lifetime);
        var window = new MainWindow { DataContext = shell, Lifetime = lifetime };
        lifetime.Window = window;
        if (StartupArgs.IsHidden(desktop.Args))
        {
            // Without a main window the lifetime shows none; the tray and the poller run all the same.
            platform.Dock.SetVisible(false);
        }
        else
        {
            desktop.MainWindow = window;
        }

        var tray = new TrayController(this, services, shell);
        lifetime.Shown += () =>
        {
            shell.OnShown(() => new AccountPickerViewModel(services));
            tray.Rebuild();
        };
        lifetime.Hidden += () =>
        {
            shell.OnHidden();
            tray.Rebuild();
        };

        // A click on the Dock icon, which macOS shows while the window is up and after a restart from the Finder.
        if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
        {
            activatable.Activated += (_, e) =>
            {
                if (e.Kind == ActivationKind.Reopen)
                {
                    lifetime.ShowWindow();
                }
            };
        }

        var activation = SingleInstance.Listen(lifetime.ShowWindow, services.Log);
        desktop.Exit += (_, _) =>
        {
            activation.Dispose();
            tray.Dispose();
        };
    }
}
