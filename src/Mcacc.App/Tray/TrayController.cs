using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Mcacc.App.Services;
using Mcacc.App.ViewModels;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.App.Tray;

/// <summary>
/// The icon in the notification area or the menu bar and its menu. It turns <see cref="TrayMenuModel"/> into native
/// menu items and carries out what they ask for; what the menu says is decided there, not here.
/// </summary>
internal sealed class TrayController : IDisposable
{
    private static readonly TimeSpan FirstTickDelay = TimeSpan.FromSeconds(5);

    private readonly AppServices _services;
    private readonly MainWindowViewModel _shell;
    private readonly LaunchCoordinator _launcher;
    private readonly TrayIcon _icon;
    private readonly NativeMenu _menu = new();

    // The last refresh per account: the cache alone keeps only the limits, not that the token has expired.
    private readonly Dictionary<string, AccountUsage> _latest = [];
    private IReadOnlySet<string> _running = new HashSet<string>();
    private bool _detecting;

    public TrayController(Application app, AppServices services, MainWindowViewModel shell)
    {
        _services = services;
        _shell = shell;
        _launcher = new LaunchCoordinator(services);
        var isMac = services.Platform.Os == OsKind.MacOS;
        _icon = new TrayIcon
        {
            Icon = isMac ? TrayIconImage.Template() : TrayIconImage.Windows(),
            Menu = _menu,
            IsVisible = true,
        };
        if (isMac)
        {
            MacOSProperties.SetIsTemplateIcon(_icon, true);
        }
        else
        {
            // On macOS a click always opens the menu; on Windows the left click is for the window, the right for the menu.
            _icon.Clicked += OnClicked;
        }

        _menu.Opening += OnMenuOpening;
        services.Poller.Updated += OnPolled;
        shell.Navigator.Navigated += OnNavigated;
        shell.PropertyChanged += OnShellChanged;
        TrayIcon.SetIcons(app, [_icon]);
        Rebuild();
        _ = RefreshRunningAsync();
        _ = RunAsync("Reading the limits for the tray", FirstTickAsync);
    }

    /// <summary>Also after the window was shown or hidden: the last account or the running state may have changed.</summary>
    public void Rebuild()
    {
        var accounts = _services.Accounts.Config.Accounts;
        var lastId = _services.Accounts.Config.Settings.LastAccountId;
        _menu.Items.Clear();
        foreach (var entry in TrayMenuModel.Build(accounts, UsageOf, _running, lastId))
        {
            _menu.Items.Add(ToNative(entry));
        }

        _icon.ToolTipText = TrayMenuModel.ToolTip(accounts, UsageOf, lastId);
    }

    public void Dispose()
    {
        _services.Poller.Updated -= OnPolled;
        _shell.Navigator.Navigated -= OnNavigated;
        _shell.PropertyChanged -= OnShellChanged;
        _menu.Opening -= OnMenuOpening;
        _icon.Clicked -= OnClicked;
        _icon.Dispose();
    }

    private NativeMenuItemBase ToNative(TrayEntry entry)
    {
        if (entry.IsSeparator)
        {
            return new NativeMenuItemSeparator();
        }

        var item = new NativeMenuItem(entry.Header) { IsEnabled = entry.IsEnabled };
        if (entry.Children is { } children)
        {
            var submenu = new NativeMenu();
            foreach (var child in children)
            {
                submenu.Items.Add(ToNative(child));
            }

            item.Menu = submenu;
        }
        else if (entry.Action != TrayAction.None)
        {
            item.Click += (_, _) => Execute(entry);
        }

        return item;
    }

    private void Execute(TrayEntry entry)
    {
        switch (entry.Action)
        {
            case TrayAction.Open:
                _services.Lifetime.ShowWindow();
                break;
            case TrayAction.LaunchDesktop or TrayAction.LaunchCode:
                Launch(entry);
                break;
            case TrayAction.Refresh:
                _ = RunAsync("Refreshing the limits from the tray", ct => _services.Poller.TickAsync(ct));
                break;
            case TrayAction.Settings:
                _services.Lifetime.ShowWindow();
                if (_services.Navigator.CurrentPage is not SettingsViewModel)
                {
                    _services.Navigator.NavigateTo(new SettingsViewModel(_services));
                }

                break;
            case TrayAction.Quit:
                _services.Lifetime.Shutdown();
                break;
        }
    }

    /// <summary>A start already under way in the window wins; the window is shown so it is clear why nothing happens.</summary>
    private void Launch(TrayEntry entry)
    {
        if (_services.Accounts.Config.Accounts.FirstOrDefault(account => account.Id == entry.AccountId) is not { } account)
        {
            return;
        }

        if (_shell.IsBusy)
        {
            _services.Lifetime.ShowWindow();
            return;
        }

        _ = RunAsync($"Starting '{account.Id}' from the tray", async _ =>
        {
            await (entry.Action == TrayAction.LaunchDesktop
                ? _launcher.LaunchDesktopAsync(account)
                : _launcher.LaunchCodeAsync(account)).ConfigureAwait(true);
            await RefreshRunningAsync().ConfigureAwait(true);
        });
    }

    /// <summary>Menu clicks have no one to hand an exception to, so a failure is logged and the tray stays.</summary>
    private async Task RunAsync(string what, Func<CancellationToken, Task> work)
    {
        try
        {
            await work(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _services.Log.Warn($"{what} failed.", e);
        }

        Rebuild();
    }

    /// <summary>One detection at a time: the poller reports each account on its own, but one look covers them all.</summary>
    private async Task RefreshRunningAsync()
    {
        if (_detecting)
        {
            return;
        }

        _detecting = true;
        try
        {
            _running = await RunningAccounts.DetectAsync(_services, CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _detecting = false;
        }

        Rebuild();
    }

    /// <summary>
    /// The start page refreshes every account as the app starts; one tick a little later gets those results through
    /// the one-minute guard of the usage service instead of asking the endpoint a second time.
    /// </summary>
    private async Task FirstTickAsync(CancellationToken ct)
    {
        await Task.Delay(FirstTickDelay, ct).ConfigureAwait(true);
        await _services.Poller.TickAsync(ct).ConfigureAwait(true);
    }

    private AccountUsage? UsageOf(Account account)
    {
        if (_latest.TryGetValue(account.Id, out var latest))
        {
            return latest;
        }

        try
        {
            return _services.Usage.GetCached(account);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _services.Log.Warn($"Reading the cached limits of '{account.Id}' failed.", e);
            return null;
        }
    }

    private void OnClicked(object? sender, EventArgs e) => _services.Lifetime.ShowWindow();

    private void OnMenuOpening(object? sender, EventArgs e) => Rebuild();

    private void OnPolled(Account account, AccountUsage usage)
    {
        _latest[account.Id] = usage;
        Rebuild();
        _ = RefreshRunningAsync();
    }

    private void OnNavigated(PageViewModel page) => Rebuild();

    /// <summary>A closed dialog may have added, renamed or removed an account.</summary>
    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.Dialog) && _shell.Dialog is null)
        {
            Rebuild();
        }
    }
}
