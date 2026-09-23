using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Mcacc.App.Services;

/// <summary>Closes the window from a view model without the view models knowing Avalonia's lifetime types.</summary>
public sealed class DesktopAppLifetime(IClassicDesktopStyleApplicationLifetime lifetime) : IAppLifetime
{
    public void Shutdown() => Dispatcher.UIThread.Post(() => lifetime.Shutdown());
}
