using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Terminal;

namespace Mcacc.App.ViewModels;

/// <summary>The second page: Claude Desktop or Claude Code, for the account picked before.</summary>
public sealed partial class TargetPickerViewModel : PageViewModel
{
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(400);

    private readonly AppServices _services;
    private DesktopLaunchCheck _check = new DesktopLaunchCheck.Ready();

    public TargetPickerViewModel(AppServices services, Account account)
    {
        _services = services;
        Account = account;
        DesktopSubtitle = Texts.DesktopReady;
        CodeSubtitle = CodeSubtitleFor();
    }

    public Account Account { get; }

    public string Title => Texts.TargetPickerTitle;

    public string Subtitle => Texts.TargetPickerSubtitle;

    public string AccountName => Account.Name;

    public string BackTooltip => Texts.Back;

    public string Initials => AccountTileViewModel.InitialsOf(Account.Name);

    public IBrush AvatarBrush => new SolidColorBrush(Color.Parse(Account.Color));

    public string DesktopTitle => Texts.TargetDesktop;

    public string CodeTitle => Texts.TargetCode;

    [ObservableProperty]
    public partial string DesktopSubtitle { get; private set; }

    [ObservableProperty]
    public partial string CodeSubtitle { get; private set; }

    [ObservableProperty]
    public partial bool IsDesktopAvailable { get; private set; } = true;

    /// <summary>Which tile the page focuses first: the target used last.</summary>
    public LaunchTarget PreferredTarget => _services.Accounts.Config.Settings.LastTarget ?? LaunchTarget.Desktop;

    public override async Task ActivateAsync(CancellationToken ct)
    {
        CodeSubtitle = CodeSubtitleFor();
        await RefreshDesktopStateAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    public async Task LaunchDesktopAsync()
    {
        var accounts = _services.Accounts.Config.Accounts;
        var check = await PrepareAsync(CancellationToken.None).ConfigureAwait(true);
        if (check is DesktopLaunchCheck.NotInstalled)
        {
            _services.Shell.ShowError(Texts.ErrorDesktopNotFound);
            return;
        }

        var quitConflicts = false;
        if (check is DesktopLaunchCheck.Conflict conflict)
        {
            var dialog = new ConfirmDialogViewModel(
                Texts.ConflictDialogTitle,
                Texts.ConflictDialogText(OwnerName(conflict), Account.Name),
                Texts.ConflictDialogRestart);
            if (await _services.Dialogs.ShowAsync(dialog).ConfigureAwait(true) != true)
            {
                return;
            }

            quitConflicts = true;
        }

        DesktopLaunchOutcome outcome;
        using (_services.Dialogs.Busy(quitConflicts ? Texts.BusyQuitting : Texts.BusyStarting))
        {
            try
            {
                outcome = await _services.Desktop
                    .LaunchAsync(Account, accounts, quitConflicts, CancellationToken.None)
                    .ConfigureAwait(true);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _services.Log.Error($"Launching Claude Desktop for '{Account.Id}' failed.", e);
                outcome = DesktopLaunchOutcome.StartFailed;
            }
        }

        switch (outcome)
        {
            case DesktopLaunchOutcome.Launched:
                _services.Log.Info($"Claude Desktop started for '{Account.Id}'.");
                await FinishAsync(LaunchTarget.Desktop, autoClose: true).ConfigureAwait(true);
                return;
            case DesktopLaunchOutcome.NotInstalled:
                _services.Shell.ShowError(Texts.ErrorDesktopNotFound);
                return;
            case DesktopLaunchOutcome.QuitFailed:
                _services.Shell.ShowError(Texts.ErrorQuitFailed);
                return;
            case DesktopLaunchOutcome.ConflictNotResolved:
                return;
            default:
                _services.Shell.ShowError(Texts.ErrorDesktopStartFailed);
                return;
        }
    }

    [RelayCommand]
    public async Task LaunchCodeAsync()
    {
        CodeLaunchResult result;
        try
        {
            result = _services.Code.Launch(Account, _services.Accounts.Config.Settings);
        }
        catch (Exception e)
        {
            _services.Log.Error($"Launching Claude Code for '{Account.Id}' failed.", e);
            _services.Shell.ShowError(Texts.ErrorTerminalStartFailed);
            return;
        }

        switch (result.Outcome)
        {
            case CodeLaunchOutcome.NoTerminal:
                _services.Shell.ShowError(Texts.ErrorNoTerminal);
                return;
            case CodeLaunchOutcome.StartFailed:
                _services.Shell.ShowError(Texts.ErrorTerminalStartFailed);
                return;
            default:
                _services.Log.Info($"Claude Code started for '{Account.Id}' in '{result.Terminal?.Id}'.");
                if (result.ClaudeCliNotFound)
                {
                    // The login shell may still find claude, so this is a hint and the switcher stays open.
                    _services.Shell.ShowError(Texts.HintClaudeCliMissing);
                }

                await FinishAsync(LaunchTarget.Code, autoClose: !result.ClaudeCliNotFound).ConfigureAwait(true);
                return;
        }
    }

    [RelayCommand]
    public void Back() => _services.Navigator.Back();

    public override bool OnKeyDown(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case Key.D when modifiers == KeyModifiers.None:
                LaunchDesktopCommand.Execute(null);
                return true;
            case Key.C when modifiers == KeyModifiers.None:
                LaunchCodeCommand.Execute(null);
                return true;
            case Key.Escape or Key.Back:
            case Key.Left when modifiers.HasFlag(KeyModifiers.Alt):
                Back();
                return true;
            default:
                return false;
        }
    }

    private string OwnerName(DesktopLaunchCheck.Conflict conflict) =>
        conflict.Conflicts.Select(c => c.Owner?.Name).FirstOrDefault(name => name is not null) ?? Account.Name;

    private async Task<DesktopLaunchCheck> PrepareAsync(CancellationToken ct)
    {
        try
        {
            _check = await _services.Desktop
                .PrepareAsync(Account, _services.Accounts.Config.Accounts, ct)
                .ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _services.Log.Warn("Checking the running instances failed.", e);
            _check = new DesktopLaunchCheck.Ready();
        }

        return _check;
    }

    private async Task RefreshDesktopStateAsync(CancellationToken ct)
    {
        var check = await PrepareAsync(ct).ConfigureAwait(true);
        IsDesktopAvailable = check is not DesktopLaunchCheck.NotInstalled;
        DesktopSubtitle = check switch
        {
            DesktopLaunchCheck.NotInstalled => Texts.DesktopNotInstalled,
            DesktopLaunchCheck.RunningSameAccount => Texts.DesktopRunningSameAccount,
            DesktopLaunchCheck.Conflict conflict => conflict.Conflicts.Any(c => c.Owner is null)
                ? Texts.DesktopConflictUnknownOwner
                : Texts.DesktopConflict(OwnerName(conflict)),
            _ => Texts.DesktopReady,
        };
    }

    private string CodeSubtitleFor()
    {
        var installed = _services.Terminals.GetInstalled();
        var settings = _services.Accounts.Config.Settings;
        var terminal = TerminalSelector.Select(settings.Terminal, installed, _services.Terminals.PlatformDefaultId);
        return terminal is null ? Texts.CodeNoTerminal : Texts.CodeInTerminal(terminal.DisplayName);
    }

    private async Task FinishAsync(LaunchTarget target, bool autoClose)
    {
        try
        {
            _services.Accounts.UpdateSettings(settings => settings with
            {
                LastAccountId = Account.Id,
                LastTarget = target,
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _services.Log.Warn("Saving the last choice failed.", e);
        }

        if (autoClose && _services.Accounts.Config.Settings.CloseAfterLaunch)
        {
            await Task.Delay(CloseDelay).ConfigureAwait(true);
            _services.Lifetime.Shutdown();
        }
    }
}
