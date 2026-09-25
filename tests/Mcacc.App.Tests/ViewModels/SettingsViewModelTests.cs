using Avalonia.Input;
using Mcacc.App.Localization;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.Model;
using Mcacc.Core.Terminal;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

public sealed class SettingsViewModelTests : IDisposable
{
    private static readonly TerminalOption WindowsTerminal = new(TerminalIds.WindowsTerminal, "Windows Terminal", "wt.exe");
    private static readonly TerminalOption Pwsh = new(TerminalIds.Pwsh, "PowerShell 7", "pwsh.exe");
    private static readonly TerminalOption WindowsPowerShell =
        new(TerminalIds.WindowsPowerShell, "Windows PowerShell", "powershell.exe");
    private static readonly TerminalOption Cmd = new(TerminalIds.Cmd, "Eingabeaufforderung", "cmd.exe");

    private readonly TestServices _services = new(OsKind.Windows, Account.CreateDefault());

    public void Dispose() => _services.Dispose();

    private SettingsViewModel Page() => new(_services.Services);

    [Fact]
    public void The_first_terminal_entry_is_the_platform_default()
    {
        _services.Terminals.Installed = [WindowsTerminal, Cmd];

        var page = Page();

        page.Terminals[0].Option.ShouldBeNull();
        page.Terminals[0].Label.ShouldBe(Texts.SettingsTerminalDefault("Windows Terminal"));
        page.SelectedTerminal.ShouldBe(page.Terminals[0]);
    }

    [Fact]
    public void Only_installed_terminals_are_offered()
    {
        _services.Terminals.Installed = [WindowsPowerShell, Cmd];

        Page().Terminals.Skip(1).Select(choice => choice.Option!.Id).ShouldBe([TerminalIds.WindowsPowerShell, TerminalIds.Cmd]);
    }

    [Fact]
    public void A_saved_terminal_is_preselected()
    {
        _services.Terminals.Installed = [WindowsTerminal, Cmd];
        _services.Accounts.UpdateSettings(settings => settings with { Terminal = TerminalIds.Cmd });

        Page().SelectedTerminal!.Option.ShouldBe(Cmd);
    }

    [Fact]
    public void A_saved_terminal_that_vanished_falls_back_to_the_default_entry()
    {
        _services.Terminals.Installed = [WindowsPowerShell];
        _services.Accounts.UpdateSettings(settings => settings with { Terminal = TerminalIds.Pwsh });

        Page().SelectedTerminal!.Option.ShouldBeNull();
    }

    [Fact]
    public void Choosing_a_terminal_saves_it_right_away()
    {
        _services.Terminals.Installed = [WindowsTerminal, Cmd];
        var page = Page();

        page.SelectedTerminal = page.Terminals.Single(choice => choice.Option?.Id == TerminalIds.Cmd);

        _services.Accounts.Config.Settings.Terminal.ShouldBe(TerminalIds.Cmd);
    }

    [Fact]
    public void Choosing_the_default_entry_saves_no_terminal()
    {
        _services.Terminals.Installed = [WindowsTerminal, Cmd];
        _services.Accounts.UpdateSettings(settings => settings with { Terminal = TerminalIds.Cmd });
        var page = Page();

        page.SelectedTerminal = page.Terminals[0];

        _services.Accounts.Config.Settings.Terminal.ShouldBeNull();
    }

    [Fact]
    public void Loading_the_page_saves_nothing()
    {
        _services.Terminals.Installed = [WindowsTerminal, Cmd];
        _services.Accounts.UpdateSettings(settings => settings with { Terminal = TerminalIds.Cmd, CloseAfterLaunch = false });

        Page();

        _services.Accounts.Config.Settings.Terminal.ShouldBe(TerminalIds.Cmd);
        _services.Accounts.Config.Settings.CloseAfterLaunch.ShouldBeFalse();
    }

    [Fact]
    public void The_shell_choice_shows_only_for_windows_terminal()
    {
        _services.Terminals.Installed = [WindowsTerminal, Cmd];
        var page = Page();
        page.IsShellVisible.ShouldBeTrue();

        page.SelectedTerminal = page.Terminals.Single(choice => choice.Option?.Id == TerminalIds.Cmd);

        page.IsShellVisible.ShouldBeFalse();
    }

    [Fact]
    public void The_shell_choice_is_hidden_on_macos()
    {
        using var mac = new TestServices(OsKind.MacOS, Account.CreateDefault());
        mac.Terminals.Installed = [new TerminalOption(TerminalIds.TerminalApp, "Terminal", "/Terminal.app")];
        mac.Terminals.PlatformDefaultId = TerminalIds.TerminalApp;

        new SettingsViewModel(mac.Services).IsShellVisible.ShouldBeFalse();
    }

    [Fact]
    public void Powershell_seven_is_only_offered_when_installed()
    {
        _services.Terminals.Installed = [WindowsTerminal, WindowsPowerShell, Cmd];

        Page().Shells.Select(choice => choice.Shell).ShouldNotContain(WindowsTerminalShell.Pwsh);

        _services.Terminals.Installed = [WindowsTerminal, Pwsh, WindowsPowerShell, Cmd];

        Page().Shells.Select(choice => choice.Shell).ShouldContain(WindowsTerminalShell.Pwsh);
    }

    [Fact]
    public void The_shells_carry_their_german_labels()
    {
        _services.Terminals.Installed = [WindowsTerminal, Pwsh, Cmd];

        Page().Shells.Select(choice => choice.Label).ShouldBe([
            Texts.SettingsShellAuto,
            Texts.SettingsShellPwsh,
            Texts.SettingsShellWindowsPowerShell,
            Texts.SettingsShellCmd,
        ]);
    }

    [Fact]
    public void Choosing_a_shell_saves_it()
    {
        _services.Terminals.Installed = [WindowsTerminal, Pwsh, Cmd];
        var page = Page();

        page.SelectedShell = page.Shells.Single(choice => choice.Shell == WindowsTerminalShell.Cmd);

        _services.Accounts.Config.Settings.WindowsTerminalShell.ShouldBe(WindowsTerminalShell.Cmd);
    }

    [Fact]
    public void Toggling_close_after_launch_saves_it()
    {
        var page = Page();

        page.CloseAfterLaunch = false;

        _services.Accounts.Config.Settings.CloseAfterLaunch.ShouldBeFalse();
    }

    [Fact]
    public void Launch_at_login_starts_from_the_saved_setting()
    {
        _services.Accounts.UpdateSettings(settings => settings with { LaunchAtLogin = false });

        Page().LaunchAtLogin.ShouldBeFalse();
        _services.LoginItem.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Loading_the_page_leaves_the_entry_alone()
    {
        Page().LaunchAtLogin.ShouldBeTrue();

        _services.LoginItem.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Switching_launch_at_login_saves_it_and_updates_the_entry()
    {
        var page = Page();

        page.LaunchAtLogin = false;
        _services.Accounts.Config.Settings.LaunchAtLogin.ShouldBeFalse();
        _services.LoginItem.Calls.ShouldBe(["disable"]);

        page.LaunchAtLogin = true;
        _services.Accounts.Config.Settings.LaunchAtLogin.ShouldBeTrue();
        _services.LoginItem.Enabled.ShouldBe(TestServices.ExecutablePath);
    }

    [Fact]
    public void The_data_folder_is_the_mcacc_root()
    {
        Page().DataFolder.ShouldBe(_services.Paths.Root);
    }

    [Fact]
    public void Revealing_the_data_folder_creates_it_first()
    {
        var page = Page();

        page.RevealDataFolderCommand.Execute(null);

        Directory.Exists(_services.Paths.Root).ShouldBeTrue();
        _services.Integration.Revealed.ShouldBe([_services.Paths.Root]);
    }

    [Fact]
    public void The_reveal_button_is_named_for_the_platform()
    {
        Page().RevealText.ShouldBe(Texts.RevealInExplorer);

        using var mac = new TestServices(OsKind.MacOS, Account.CreateDefault());
        mac.Terminals.PlatformDefaultId = TerminalIds.TerminalApp;

        new SettingsViewModel(mac.Services).RevealText.ShouldBe(Texts.RevealInFinder);
    }

    [Fact]
    public void The_version_is_shown()
    {
        Page().VersionText.ShouldBe(Texts.Version("1.2.3"));
    }

    [Fact]
    public void Escape_goes_back()
    {
        var picker = new AccountPickerViewModel(_services.Services);
        _services.Navigator.Reset(picker);
        var page = Page();
        _services.Navigator.NavigateTo(page);

        page.OnKeyDown(Key.Escape, KeyModifiers.None).ShouldBeTrue();

        _services.Navigator.CurrentPage.ShouldBe(picker);
    }
}
