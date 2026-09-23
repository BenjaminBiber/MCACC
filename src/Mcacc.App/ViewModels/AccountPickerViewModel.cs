using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;

namespace Mcacc.App.ViewModels;

/// <summary>The first page: which account Claude should start with.</summary>
public sealed partial class AccountPickerViewModel : PageViewModel
{
    private readonly AppServices _services;

    public AccountPickerViewModel(AppServices services)
    {
        _services = services;
        Reload();
    }

    public ObservableCollection<AccountTileViewModel> Tiles { get; } = [];

    /// <summary>The tile the page focuses first: the account used last.</summary>
    [ObservableProperty]
    public partial AccountTileViewModel? PreferredTile { get; private set; }

    public string Title => Texts.AccountPickerTitle;

    public string Subtitle => Texts.AccountPickerSubtitle;

    public string AddText => Texts.AddAccount;

    public override async Task ActivateAsync(CancellationToken ct)
    {
        Reload();
        await Task.WhenAll(RefreshRunningAsync(ct), RefreshUsageAsync(ct)).ConfigureAwait(true);
    }

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

    public override bool OnKeyDown(Key key, KeyModifiers modifiers)
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
            case >= Key.D1 and <= Key.D9 when !command:
                Select(Tiles.ElementAtOrDefault(key - Key.D1));
                return true;
            default:
                return false;
        }
    }

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
            tiles[i].SetUsage(results[i]);
        }
    }

    private async Task RefreshRunningAsync(CancellationToken ct)
    {
        var instances = await DetectAsync(ct).ConfigureAwait(true);
        var owners = instances
            .Select(instance => _services.Matcher.Match(instance, _services.Accounts.Config.Accounts)?.Id)
            .Where(id => id is not null)
            .ToHashSet();
        foreach (var tile in Tiles)
        {
            tile.IsRunning = owners.Contains(tile.Id);
        }
    }

    private async Task<bool> IsRunningAsync(Account account, CancellationToken ct)
    {
        var instances = await DetectAsync(ct).ConfigureAwait(true);
        return instances.Any(instance =>
            _services.Matcher.Match(instance, _services.Accounts.Config.Accounts)?.Id == account.Id);
    }

    private async Task<IReadOnlyList<DesktopInstance>> DetectAsync(CancellationToken ct)
    {
        try
        {
            return await _services.Detector.DetectAsync(ct).ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _services.Log.Warn("Detecting the running instances failed.", e);
            return [];
        }
    }
}
