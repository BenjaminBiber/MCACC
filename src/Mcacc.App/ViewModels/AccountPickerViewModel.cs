using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.App.ViewModels;

/// <summary>The first page: which account Claude should start with.</summary>
public sealed partial class AccountPickerViewModel : PageViewModel
{
    private readonly AppServices _services;

    public AccountPickerViewModel(AppServices services)
    {
        _services = services;
        AddTile = new AddAccountTileViewModel(Texts.AddAccount, AddCommand);
        Entries = [AddTile];
        Tiles.CollectionChanged += MirrorTiles;
        Reload();
    }

    public ObservableCollection<AccountTileViewModel> Tiles { get; } = [];

    public AddAccountTileViewModel AddTile { get; }

    /// <summary>What the grid shows: the tiles, then the add tile, so it sits in the same columns.</summary>
    public ObservableCollection<ViewModelBase> Entries { get; }

    /// <summary>The tile the page focuses first: the account used last.</summary>
    [ObservableProperty]
    public partial AccountTileViewModel? PreferredTile { get; private set; }

    public string Title => Texts.AccountPickerTitle;

    public string Subtitle => Texts.AccountPickerSubtitle;

    public string RefreshTooltip => Texts.RefreshUsage;

    public string SettingsTooltip => Texts.Settings;

    public string MoreTooltip => Texts.More;

    public string InfoText => Texts.Info;

    public string EditText => Texts.Edit;

    public string RemoveText => Texts.Remove;

    public string OpenFolderText => Texts.OpenFolder;

    public override async Task ActivateAsync(CancellationToken ct)
    {
        _services.Poller.Updated -= OnPolled;
        _services.Poller.Updated += OnPolled;
        Reload();
        await Task.WhenAll(RefreshRunningAsync(ct), RefreshUsageAsync(ct)).ConfigureAwait(true);
    }

    public override void Deactivate() => _services.Poller.Updated -= OnPolled;

    [RelayCommand]
    public void Select(AccountTileViewModel? tile)
    {
        if (tile is not null)
        {
            _services.Navigator.NavigateTo(new TargetPickerViewModel(_services, tile.Account));
        }
    }

    [RelayCommand]
    public async Task AddAsync()
    {
        var editor = AccountEditorViewModel.ForCreate(_services);
        if (await _services.Dialogs.ShowAsync(editor).ConfigureAwait(true) is null)
        {
            return;
        }

        Reload();
        await RefreshUsageAsync(CancellationToken.None).ConfigureAwait(true);
    }

    [RelayCommand]
    public void OpenInfo(AccountTileViewModel? tile)
    {
        if (tile is not null)
        {
            _services.Navigator.NavigateTo(new AccountInfoViewModel(_services, tile.Account));
        }
    }

    [RelayCommand]
    public async Task EditAsync(AccountTileViewModel? tile)
    {
        if (tile is null)
        {
            return;
        }

        var editor = AccountEditorViewModel.ForEdit(_services, tile.Account);
        if (await _services.Dialogs.ShowAsync(editor).ConfigureAwait(true) is not null)
        {
            Reload();
        }
    }

    [RelayCommand]
    public async Task RemoveAsync(AccountTileViewModel? tile)
    {
        if (tile is null || tile.IsDefault)
        {
            return;
        }

        var dialog = new RemoveAccountDialogViewModel(_services, tile.Account);
        var result = await _services.Dialogs.ShowAsync(dialog).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        if (result.DeleteData && await IsRunningAsync(tile.Account, CancellationToken.None).ConfigureAwait(true))
        {
            _services.Shell.ShowError(Texts.ErrorRunningWithThisAccount);
            return;
        }

        try
        {
            _services.Accounts.Remove(tile.Account.Id, result.DeleteData);
            // The account is gone at this point, so its statistics go too, whatever forgetting the limits hit.
            try
            {
                _services.Usage.Forget(tile.Account);
            }
            finally
            {
                _services.Stats.Forget(tile.Account);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _services.Log.Error($"Removing account '{tile.Account.Id}' failed.", e);
            _services.Shell.ShowError(e.Message);
        }

        Reload();
    }

    [RelayCommand]
    public void OpenFolder(AccountTileViewModel? tile)
    {
        if (tile is null)
        {
            return;
        }

        var paths = _services.Resolver.Resolve(tile.Account);
        var folder = paths.DesktopUserDataDir is null
            ? paths.EffectiveClaudeDir
            : _services.Paths.ProfileDir(tile.Account.Id);
        try
        {
            _services.Integration.RevealFolder(folder);
        }
        catch (Exception e)
        {
            _services.Log.Warn($"Revealing '{folder}' failed.", e);
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await RefreshUsageAsync(CancellationToken.None).ConfigureAwait(true);
    }

    [RelayCommand]
    public void OpenSettings() => _services.Navigator.NavigateTo(new SettingsViewModel(_services));

    /// <param name="focused">The tile the view has focused, for the keys that act on one.</param>
    public bool OnKeyDown(Key key, KeyModifiers modifiers, AccountTileViewModel? focused)
    {
        var command = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        switch (key)
        {
            case Key.N when command:
                AddCommand.Execute(null);
                return true;
            case Key.OemComma when command:
                OpenSettingsCommand.Execute(null);
                return true;
            case Key.F2 when focused is not null:
                EditCommand.Execute(focused);
                return true;
            case Key.Delete when focused is { IsDefault: false }:
                RemoveCommand.Execute(focused);
                return true;
            case Key.Escape:
                _services.Lifetime.HideWindow();
                return true;
            case >= Key.D1 and <= Key.D9 when !command:
                Select(Tiles.ElementAtOrDefault(key - Key.D1));
                return true;
            default:
                return false;
        }
    }

    public override bool OnKeyDown(Key key, KeyModifiers modifiers) => OnKeyDown(key, modifiers, focused: null);

    private void Reload()
    {
        var accounts = _services.Accounts.Config.Accounts;
        for (var i = Tiles.Count - 1; i >= 0; i--)
        {
            if (accounts.All(account => account.Id != Tiles[i].Id))
            {
                Tiles.RemoveAt(i);
            }
        }

        for (var i = 0; i < accounts.Count; i++)
        {
            var account = accounts[i];
            var existing = Tiles.FirstOrDefault(tile => tile.Id == account.Id);
            if (existing is null)
            {
                var tile = new AccountTileViewModel(account);
                tile.SetLogin(_services.Login.Read(_services.Resolver.Resolve(account))?.Email);
                tile.SetUsage(_services.Usage.GetCached(account));
                Tiles.Insert(Math.Min(i, Tiles.Count), tile);
            }
            else
            {
                existing.Update(account);
            }
        }

        var lastId = _services.Accounts.Config.Settings.LastAccountId;
        PreferredTile = Tiles.FirstOrDefault(tile => tile.Id == lastId) ?? Tiles.FirstOrDefault();
    }

    /// <summary>
    /// Applies each change of <see cref="Tiles"/> to <see cref="Entries"/> at the same index, since the add tile stays last;
    /// a rebuild would recreate every tile and drop the keyboard focus.
    /// </summary>
    private void MirrorTiles(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                for (var i = 0; i < e.NewItems!.Count; i++)
                {
                    Entries.Insert(e.NewStartingIndex + i, (AccountTileViewModel)e.NewItems[i]!);
                }

                break;
            case NotifyCollectionChangedAction.Remove:
                for (var i = 0; i < e.OldItems!.Count; i++)
                {
                    Entries.RemoveAt(e.OldStartingIndex);
                }

                break;
            default:
                Entries.Clear();
                foreach (var tile in Tiles)
                {
                    Entries.Add(tile);
                }

                Entries.Add(AddTile);
                break;
        }
    }

    /// <summary>Errors are logged only: a failing refresh must not interrupt the picker.</summary>
    private async Task RefreshUsageAsync(CancellationToken ct)
    {
        var tiles = Tiles.ToList();
        var results = await Task.WhenAll(tiles.Select(async tile =>
        {
            try
            {
                return await _services.Usage.RefreshAsync(tile.Account, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _services.Log.Warn($"Reading the limits of '{tile.Id}' failed.", e);
                return _services.Usage.GetCached(tile.Account);
            }
        })).ConfigureAwait(true);

        for (var i = 0; i < tiles.Count; i++)
        {
            if (results[i].State == UsageState.WrongAccount)
            {
                _services.Log.Warn($"The Claude Code token of '{tiles[i].Id}' belongs to another account.");
            }

            tiles[i].SetUsage(results[i]);
        }
    }

    private void OnPolled(Account account, AccountUsage usage) =>
        Tiles.FirstOrDefault(tile => tile.Id == account.Id)?.SetUsage(usage);

    private async Task RefreshRunningAsync(CancellationToken ct)
    {
        var owners = await RunningAccounts.DetectAsync(_services, ct).ConfigureAwait(true);
        foreach (var tile in Tiles)
        {
            tile.IsRunning = owners.Contains(tile.Id);
        }
    }

    private async Task<bool> IsRunningAsync(Account account, CancellationToken ct) =>
        (await RunningAccounts.DetectAsync(_services, ct).ConfigureAwait(true)).Contains(account.Id);
}
