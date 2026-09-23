using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.Core.Model;
using Mcacc.Core.Terminal;

namespace Mcacc.App.ViewModels;

/// <param name="Option">Null is the platform default.</param>
public sealed record TerminalChoice(TerminalOption? Option, string Label);

public sealed record ShellChoice(WindowsTerminalShell Shell, string Label);

/// <summary>The settings page. Every change is saved right away, so there is no save button.</summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly AppServices _services;
    private readonly bool _isLoading;

    public SettingsViewModel(AppServices services)
    {
        _isLoading = true;
        _services = services;
        var installed = services.Terminals.GetInstalled();
        var platformDefault = installed.FirstOrDefault(option => option.Id == services.Terminals.PlatformDefaultId)
                              ?? installed.FirstOrDefault();
        var defaultLabel = Texts.SettingsTerminalDefault(platformDefault?.DisplayName ?? Texts.CodeNoTerminal);
        Terminals =
        [
            new TerminalChoice(null, defaultLabel),
            .. installed.Select(option => new TerminalChoice(option, option.DisplayName)),
        ];
        Shells = ShellsFor(installed);

        var settings = services.Accounts.Config.Settings;
        SelectedTerminal = Terminals.FirstOrDefault(choice => choice.Option?.Id == settings.Terminal) ?? Terminals[0];
        SelectedShell = Shells.FirstOrDefault(choice => choice.Shell == settings.WindowsTerminalShell) ?? Shells[0];
        CloseAfterLaunch = settings.CloseAfterLaunch;
        _isLoading = false;
    }

    public string Title => Texts.SettingsTitle;

    public string BackTooltip => Texts.Back;

    public string TerminalLabel => Texts.SettingsTerminal;

    public string ShellLabel => Texts.SettingsShell;

    public string CloseAfterLaunchText => Texts.SettingsCloseAfterLaunch;

    public string DataFolderLabel => Texts.SettingsDataFolder;

    public string DataFolder => _services.Paths.Root;

    public string RevealText => _services.Platform.Os == OsKind.MacOS ? Texts.RevealInFinder : Texts.RevealInExplorer;

    public string VersionText => Texts.Version(_services.Version);

    public IReadOnlyList<TerminalChoice> Terminals { get; }

    public IReadOnlyList<ShellChoice> Shells { get; }

    /// <summary>The shell only matters when the launch actually goes through Windows Terminal.</summary>
    public bool IsShellVisible =>
        _services.Platform.Os == OsKind.Windows && SelectedOrDefaultTerminalId() == TerminalIds.WindowsTerminal;

    [ObservableProperty]
    public partial TerminalChoice? SelectedTerminal { get; set; }

    [ObservableProperty]
    public partial ShellChoice? SelectedShell { get; set; }

    [ObservableProperty]
    public partial bool CloseAfterLaunch { get; set; }

    [RelayCommand]
    public void Back() => _services.Navigator.Back();

    [RelayCommand]
    public void RevealDataFolder()
    {
        try
        {
            Directory.CreateDirectory(_services.Paths.Root);
            _services.Integration.RevealFolder(_services.Paths.Root);
        }
        catch (Exception e)
        {
            _services.Log.Warn($"Revealing '{_services.Paths.Root}' failed.", e);
        }
    }

    public override bool OnKeyDown(Key key, KeyModifiers modifiers)
    {
        if (key is not (Key.Escape or Key.Back))
        {
            return false;
        }

        Back();
        return true;
    }

    private static string LabelOf(WindowsTerminalShell shell) => shell switch
    {
        WindowsTerminalShell.Auto => Texts.SettingsShellAuto,
        WindowsTerminalShell.Pwsh => Texts.SettingsShellPwsh,
        WindowsTerminalShell.WindowsPowerShell => Texts.SettingsShellWindowsPowerShell,
        WindowsTerminalShell.Cmd => Texts.SettingsShellCmd,
        _ => throw new ArgumentOutOfRangeException(nameof(shell), shell, null),
    };

    /// <summary>PowerShell 7 is only offered when it is actually installed.</summary>
    private static IReadOnlyList<ShellChoice> ShellsFor(IReadOnlyList<TerminalOption> installed)
    {
        var hasPwsh = installed.Any(option => option.Id == TerminalIds.Pwsh);
        return
        [
            .. Enum.GetValues<WindowsTerminalShell>()
                .Where(shell => hasPwsh || shell != WindowsTerminalShell.Pwsh)
                .Select(shell => new ShellChoice(shell, LabelOf(shell))),
        ];
    }

    partial void OnSelectedTerminalChanged(TerminalChoice? value)
    {
        OnPropertyChanged(nameof(IsShellVisible));
        Save(settings => settings with { Terminal = value?.Option?.Id });
    }

    partial void OnSelectedShellChanged(ShellChoice? value) => Save(settings => settings with
    {
        WindowsTerminalShell = value?.Shell ?? WindowsTerminalShell.Auto,
    });

    partial void OnCloseAfterLaunchChanged(bool value) => Save(settings => settings with { CloseAfterLaunch = value });

    private string? SelectedOrDefaultTerminalId() => SelectedTerminal?.Option?.Id ?? _services.Terminals.PlatformDefaultId;

    private void Save(Func<AppSettings, AppSettings> change)
    {
        if (_isLoading)
        {
            return;
        }

        try
        {
            _services.Accounts.UpdateSettings(change);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _services.Log.Error("Saving the settings failed.", e);
            _services.Shell.ShowError(e.Message);
        }
    }
}
