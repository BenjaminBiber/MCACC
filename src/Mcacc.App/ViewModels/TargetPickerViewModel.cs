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
    private readonly AppServices _services;
    private readonly LaunchCoordinator _launcher;

    public TargetPickerViewModel(AppServices services, Account account)
    {
        _services = services;
        _launcher = new LaunchCoordinator(services);
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
    public Task LaunchDesktopAsync() => _launcher.LaunchDesktopAsync(Account);

    [RelayCommand]
    public Task LaunchCodeAsync() => _launcher.LaunchCodeAsync(Account);

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

    private async Task RefreshDesktopStateAsync(CancellationToken ct)
    {
        var check = await _launcher.CheckDesktopAsync(Account, ct).ConfigureAwait(true);
        IsDesktopAvailable = check is not DesktopLaunchCheck.NotInstalled;
        DesktopSubtitle = check switch
        {
            DesktopLaunchCheck.NotInstalled => Texts.DesktopNotInstalled,
            DesktopLaunchCheck.RunningSameAccount => Texts.DesktopRunningSameAccount,
            DesktopLaunchCheck.Conflict conflict => conflict.Conflicts.Any(c => c.Owner is null)
                ? Texts.DesktopConflictUnknownOwner
                : Texts.DesktopConflict(LaunchCoordinator.OwnerName(conflict, Account)),
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
}
