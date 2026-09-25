namespace Mcacc.App.Services;

/// <summary>
/// The window and the process as the view models see them. MCACC lives on in the tray, so a finished start only hides
/// the window; the process ends on <see cref="Shutdown"/> alone.
/// </summary>
public interface IAppLifetime
{
    /// <summary>Shows the window, brings it to the front and, on macOS, the Dock icon with it.</summary>
    void ShowWindow();

    /// <summary>Hides the window and, on macOS, the Dock icon; the tray icon stays.</summary>
    void HideWindow();

    void Shutdown();
}
