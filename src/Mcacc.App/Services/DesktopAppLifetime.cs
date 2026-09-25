using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Mcacc.Platform;

namespace Mcacc.App.Services;

/// <summary>Shows, hides and closes the window from a view model without the view models knowing Avalonia's types.</summary>
public sealed class DesktopAppLifetime(IClassicDesktopStyleApplicationLifetime lifetime, IDockIcon dock) : IAppLifetime
{
    /// <summary>After the window was brought up, before anything else runs on the UI thread.</summary>
    public event Action? Shown;

    /// <summary>After the window went away; the tray icon is all that is left.</summary>
    public event Action? Hidden;

    public Window? Window { get; set; }

    /// <summary>
    /// Runs at once on the UI thread, so whatever the caller does next — a dialog, a page — lands on the window as it
    /// is after <see cref="Shown"/>.
    /// </summary>
    public void ShowWindow() => OnUiThread(() =>
    {
        if (Window is not { } window)
        {
            return;
        }

        dock.SetVisible(true);
        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        Shown?.Invoke();
    });

    /// <summary>Posted, because it may be asked for from the window's own closing handler.</summary>
    public void HideWindow() => Dispatcher.UIThread.Post(() =>
    {
        if (Window is not { IsVisible: true } window)
        {
            return;
        }

        window.Hide();
        dock.SetVisible(false);
        Hidden?.Invoke();
    });

    public void Shutdown() => Dispatcher.UIThread.Post(() => lifetime.Shutdown());

    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
