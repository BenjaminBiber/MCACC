using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.Core.Model;
using Mcacc.Core.Stats;
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
    private CancellationTokenSource? _life;

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
        LaunchAtLogin = settings.LaunchAtLogin;
        _isLoading = false;
    }

    public string Title => Texts.SettingsTitle;

    public string BackTooltip => Texts.Back;

    public string TerminalLabel => Texts.SettingsTerminal;

    public string ShellLabel => Texts.SettingsShell;

    public string CloseAfterLaunchText => Texts.SettingsCloseAfterLaunch;

    public string TrayHint => Texts.SettingsTrayHint(_services.Platform.Os);

    public string LaunchAtLoginText => Texts.SettingsLaunchAtLogin;

    public string LaunchAtLoginHint => Texts.SettingsLaunchAtLoginHint(_services.Platform.Os);

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

    [ObservableProperty]
    public partial bool LaunchAtLogin { get; set; }

    public string ProjectsLabel => Texts.SettingsProjects;

    public string ProjectsHint => Texts.SettingsProjectsHint;

    /// <summary>Every project the logs of any account mention, the largest first; excluded ones no log knows last.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProjectChoiceViewModel> Projects { get; private set; } = [];

    /// <summary>What stands in place of the list: loading, failed or empty; null once there is a list.</summary>
    [ObservableProperty]
    public partial string? ProjectsStatus { get; private set; } = Texts.SettingsProjectsLoading;

    public override async Task ActivateAsync(CancellationToken ct)
    {
        _life?.Cancel();
        _life?.Dispose();
        _life = new CancellationTokenSource();
        await LoadProjectsAsync(_life.Token).ConfigureAwait(true);
    }

    public override void Deactivate() => _life?.Cancel();

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

    partial void OnLaunchAtLoginChanged(bool value)
    {
        if (Save(settings => settings with { LaunchAtLogin = value }))
        {
            _services.LoginItems.Apply(value);
        }
    }

    /// <summary>
    /// Reads the logs of every account through the stats index, like the info page, and does not cancel the scan
    /// with the page for the same reason; <paramref name="life"/> only keeps a left page from being updated. An
    /// account whose logs fail is logged and left out.
    /// </summary>
    private async Task LoadProjectsAsync(CancellationToken life)
    {
        ProjectsStatus = Projects.Count == 0 ? Texts.SettingsProjectsLoading : null;
        var buckets = new List<TokenBucket>();
        var failed = false;
        foreach (var account in _services.Accounts.Config.Accounts)
        {
            try
            {
                buckets.AddRange(await _services.Stats.LoadAsync(account, null, CancellationToken.None).ConfigureAwait(true));
            }
            catch (Exception e)
            {
                _services.Log.Warn($"Reading the Claude Code logs of '{account.Id}' for the settings failed.", e);
                failed = true;
            }
        }

        var roots = await Task.Run(() => ProjectRoot.Resolve(buckets.Select(bucket => bucket.Project)), CancellationToken.None)
            .ConfigureAwait(true);
        if (life.IsCancellationRequested)
        {
            return;
        }

        var excluded = _services.Accounts.Config.Settings.ExcludedProjects ?? [];
        var known = buckets
            .Where(bucket => bucket.Project.Length > 0)
            .GroupBy(bucket => ProjectFilter.Normalize(roots[bucket.Project]), ProjectFilter.Comparer)
            .Select(group => (Path: group.Key, Tokens: (long?)group.Sum(bucket => bucket.Tokens.Total)))
            .OrderByDescending(project => project.Tokens)
            .ThenBy(project => project.Path, ProjectFilter.Comparer)
            .ToList();
        var gone = excluded
            .Select(ProjectFilter.Normalize)
            .Distinct(ProjectFilter.Comparer)
            .Where(path => !known.Any(project => ProjectFilter.Comparer.Equals(project.Path, path)))
            .Order(ProjectFilter.Comparer)
            .Select(path => (Path: path, Tokens: (long?)null));
        Projects =
        [
            .. known.Concat(gone).Select(project => new ProjectChoiceViewModel(
                project.Path,
                project.Tokens,
                !ProjectFilter.Contains(excluded, project.Path),
                SaveExcludedProjects)),
        ];
        ProjectsStatus = Projects.Count > 0 ? null
            : failed ? Texts.SettingsProjectsFailed
            : Texts.SettingsProjectsNone;
    }

    /// <summary>Stores the whole list, so an excluded project the logs no longer know is kept as long as it is shown.</summary>
    private void SaveExcludedProjects()
    {
        var excluded = Projects.Where(project => !project.IsCounted).Select(project => project.Path).ToList();
        Save(settings => settings with { ExcludedProjects = excluded.Count > 0 ? excluded : null });
    }

    private string? SelectedOrDefaultTerminalId() => SelectedTerminal?.Option?.Id ?? _services.Terminals.PlatformDefaultId;

    /// <returns>False while loading and when the file could not be written.</returns>
    private bool Save(Func<AppSettings, AppSettings> change)
    {
        if (_isLoading)
        {
            return false;
        }

        try
        {
            _services.Accounts.UpdateSettings(change);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _services.Log.Error("Saving the settings failed.", e);
            _services.Shell.ShowError(e.Message);
            return false;
        }
    }
}
