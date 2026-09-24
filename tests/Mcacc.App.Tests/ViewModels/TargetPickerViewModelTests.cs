using Avalonia.Input;
using Mcacc.App.Localization;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Terminal;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

public sealed class TargetPickerViewModelTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TestServices _services = new(OsKind.Windows, Standard, Privat);

    public void Dispose() => _services.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private TargetPickerViewModel Page(Account? account = null) => new(_services.Services, account ?? Privat);

    private DesktopInstance StandardInstance => _services.RunningInstance(Standard, pid: 200);

    [Fact]
    public async Task A_free_desktop_offers_a_plain_start()
    {
        var page = Page();

        await page.ActivateAsync(Ct);

        page.DesktopSubtitle.ShouldBe(Texts.DesktopReady);
        page.IsDesktopAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task A_missing_installation_is_shown_on_the_tile()
    {
        _services.Desktop.Check = new DesktopLaunchCheck.NotInstalled();
        var page = Page();

        await page.ActivateAsync(Ct);

        page.DesktopSubtitle.ShouldBe(Texts.DesktopNotInstalled);
        page.IsDesktopAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task The_same_account_running_is_shown_as_being_brought_forward()
    {
        _services.Desktop.Check = new DesktopLaunchCheck.RunningSameAccount();
        var page = Page();

        await page.ActivateAsync(Ct);

        page.DesktopSubtitle.ShouldBe(Texts.DesktopRunningSameAccount);
    }

    [Fact]
    public async Task A_conflict_names_the_other_account()
    {
        _services.Desktop.Check = new DesktopLaunchCheck.Conflict([new DesktopConflict(StandardInstance, Standard)]);
        var page = Page();

        await page.ActivateAsync(Ct);

        page.DesktopSubtitle.ShouldBe(Texts.DesktopConflict("Standard"));
    }

    [Fact]
    public async Task A_conflict_of_an_unknown_profile_says_so()
    {
        _services.Desktop.Check = new DesktopLaunchCheck.Conflict([new DesktopConflict(StandardInstance, null)]);
        var page = Page();

        await page.ActivateAsync(Ct);

        page.DesktopSubtitle.ShouldBe(Texts.DesktopConflictUnknownOwner);
    }

    [Fact]
    public void The_code_tile_names_the_selected_terminal()
    {
        Page().CodeSubtitle.ShouldBe(Texts.CodeInTerminal("Windows Terminal"));
    }

    [Fact]
    public void Without_a_terminal_the_code_tile_says_so()
    {
        _services.Terminals.Installed = [];

        Page().CodeSubtitle.ShouldBe(Texts.CodeNoTerminal);
    }

    [Fact]
    public async Task Starting_the_desktop_saves_the_choice_and_closes_the_switcher()
    {
        var page = Page();

        await page.LaunchDesktopCommand.ExecuteAsync(null);

        _services.Desktop.Launches.ShouldBe([false]);
        _services.Accounts.Config.Settings.LastAccountId.ShouldBe("privat");
        _services.Accounts.Config.Settings.LastTarget.ShouldBe(LaunchTarget.Desktop);
        _services.Lifetime.Shutdowns.ShouldBe(1);
    }

    [Fact]
    public async Task With_close_after_launch_off_the_switcher_stays_open()
    {
        _services.Accounts.UpdateSettings(settings => settings with { CloseAfterLaunch = false });
        var page = Page();

        await page.LaunchDesktopCommand.ExecuteAsync(null);

        _services.Lifetime.Shutdowns.ShouldBe(0);
    }

    [Fact]
    public async Task Starting_the_desktop_for_a_profile_watches_for_a_misrouted_login()
    {
        await Page().LaunchDesktopCommand.ExecuteAsync(null);

        _services.Redirect.Watched.ShouldBe([Privat]);
    }

    [Fact]
    public async Task The_default_account_is_not_watched_because_a_stray_login_lands_in_its_profile()
    {
        await Page(Standard).LaunchDesktopCommand.ExecuteAsync(null);

        _services.Redirect.Watched.ShouldBeEmpty();
        _services.Lifetime.Shutdowns.ShouldBe(1);
    }

    [Fact]
    public async Task While_the_login_may_still_come_back_the_switcher_says_so_and_stays_open()
    {
        _services.Redirect.Gate = new TaskCompletionSource();
        var page = Page();

        var launch = page.LaunchDesktopCommand.ExecuteAsync(null);

        _services.Window.ErrorBanner.ShouldBe(Texts.HintLoginWatch);
        _services.Lifetime.Shutdowns.ShouldBe(0);

        _services.Redirect.Gate.SetResult();
        await launch;

        _services.Window.ErrorBanner.ShouldBeNull();
        _services.Lifetime.Shutdowns.ShouldBe(1);
    }

    [Fact]
    public async Task A_redirected_login_takes_the_hint_down_and_closes_the_switcher()
    {
        _services.Redirect.Outcome = DesktopRedirectOutcome.Redirected;

        await Page().LaunchDesktopCommand.ExecuteAsync(null);

        _services.Window.ErrorBanner.ShouldBeNull();
        _services.Lifetime.Shutdowns.ShouldBe(1);
    }

    [Fact]
    public async Task A_login_that_could_not_be_handed_over_is_reported_and_keeps_the_switcher_open()
    {
        _services.Redirect.Outcome = DesktopRedirectOutcome.Failed;

        await Page().LaunchDesktopCommand.ExecuteAsync(null);

        _services.Window.ErrorBanner.ShouldBe(Texts.ErrorLoginRedirectFailed);
        _services.Lifetime.Shutdowns.ShouldBe(0);
    }

    [Fact]
    public async Task A_watch_that_throws_is_reported_like_a_failed_handover()
    {
        _services.Redirect.Gate = new TaskCompletionSource();
        _services.Redirect.Gate.SetException(new InvalidOperationException("no snapshot"));

        await Page().LaunchDesktopCommand.ExecuteAsync(null);

        _services.Window.ErrorBanner.ShouldBe(Texts.ErrorLoginRedirectFailed);
        _services.Lifetime.Shutdowns.ShouldBe(0);
    }

    [Fact]
    public async Task With_close_after_launch_off_a_finished_watch_leaves_the_switcher_open()
    {
        _services.Accounts.UpdateSettings(settings => settings with { CloseAfterLaunch = false });

        await Page().LaunchDesktopCommand.ExecuteAsync(null);

        _services.Redirect.Watched.ShouldBe([Privat]);
        _services.Lifetime.Shutdowns.ShouldBe(0);
    }

    [Fact]
    public async Task A_conflict_asks_before_quitting_and_then_restarts()
    {
        _services.Desktop.Check = new DesktopLaunchCheck.Conflict([new DesktopConflict(StandardInstance, Standard)]);
        _services.Dialogs.Answer<ConfirmDialogViewModel>(_ => true);
        var page = Page();

        await page.LaunchDesktopCommand.ExecuteAsync(null);

        var dialog = _services.Dialogs.Shown.ShouldHaveSingleItem().ShouldBeOfType<ConfirmDialogViewModel>();
        dialog.Title.ShouldBe(Texts.ConflictDialogTitle);
        dialog.Text.ShouldBe(Texts.ConflictDialogText("Standard", "Privat"));
        _services.Desktop.Launches.ShouldBe([true]);
        _services.Dialogs.BusyTexts.ShouldBe([Texts.BusyQuitting]);
        _services.Lifetime.Shutdowns.ShouldBe(1);
    }

    [Fact]
    public async Task Cancelling_the_conflict_dialog_starts_nothing()
    {
        _services.Desktop.Check = new DesktopLaunchCheck.Conflict([new DesktopConflict(StandardInstance, Standard)]);
        var page = Page();

        await page.LaunchDesktopCommand.ExecuteAsync(null);

        _services.Desktop.Launches.ShouldBeEmpty();
        _services.Lifetime.Shutdowns.ShouldBe(0);
        _services.Window.ErrorBanner.ShouldBeNull();
    }

    [Fact]
    public async Task A_missing_installation_shows_an_error_instead_of_launching()
    {
        _services.Desktop.Check = new DesktopLaunchCheck.NotInstalled();
        var page = Page();

        await page.LaunchDesktopCommand.ExecuteAsync(null);

        _services.Desktop.Launches.ShouldBeEmpty();
        _services.Window.ErrorBanner.ShouldBe(Texts.ErrorDesktopNotFound);
    }

    [Fact]
    public async Task A_failed_quit_shows_an_error_and_keeps_the_switcher_open()
    {
        _services.Desktop.Check = new DesktopLaunchCheck.Conflict([new DesktopConflict(StandardInstance, Standard)]);
        _services.Desktop.Outcome = DesktopLaunchOutcome.QuitFailed;
        _services.Dialogs.Answer<ConfirmDialogViewModel>(_ => true);
        var page = Page();

        await page.LaunchDesktopCommand.ExecuteAsync(null);

        _services.Window.ErrorBanner.ShouldBe(Texts.ErrorQuitFailed);
        _services.Lifetime.Shutdowns.ShouldBe(0);
    }

    [Fact]
    public async Task A_failed_start_shows_an_error()
    {
        _services.Desktop.Outcome = DesktopLaunchOutcome.StartFailed;
        var page = Page();

        await page.LaunchDesktopCommand.ExecuteAsync(null);

        _services.Window.ErrorBanner.ShouldBe(Texts.ErrorDesktopStartFailed);
        _services.Lifetime.Shutdowns.ShouldBe(0);
    }

    [Fact]
    public async Task Starting_claude_code_saves_the_choice_and_closes_the_switcher()
    {
        var page = Page();

        await page.LaunchCodeCommand.ExecuteAsync(null);

        _services.Code.Launched.ShouldBe([Privat]);
        _services.Accounts.Config.Settings.LastTarget.ShouldBe(LaunchTarget.Code);
        _services.Lifetime.Shutdowns.ShouldBe(1);
    }

    [Fact]
    public async Task A_missing_claude_cli_only_warns_and_keeps_the_switcher_open()
    {
        _services.Code.Result = _services.Code.Result with { ClaudeCliNotFound = true };
        var page = Page();

        await page.LaunchCodeCommand.ExecuteAsync(null);

        _services.Window.ErrorBanner.ShouldBe(Texts.HintClaudeCliMissing);
        _services.Accounts.Config.Settings.LastTarget.ShouldBe(LaunchTarget.Code);
        _services.Lifetime.Shutdowns.ShouldBe(0);
    }

    [Fact]
    public async Task Without_a_terminal_claude_code_shows_an_error()
    {
        _services.Code.Result = new CodeLaunchResult(CodeLaunchOutcome.NoTerminal, null, false);
        var page = Page();

        await page.LaunchCodeCommand.ExecuteAsync(null);

        _services.Window.ErrorBanner.ShouldBe(Texts.ErrorNoTerminal);
        _services.Lifetime.Shutdowns.ShouldBe(0);
    }

    [Fact]
    public async Task A_terminal_that_will_not_start_shows_an_error()
    {
        _services.Code.Result = _services.Code.Result with { Outcome = CodeLaunchOutcome.StartFailed };
        var page = Page();

        await page.LaunchCodeCommand.ExecuteAsync(null);

        _services.Window.ErrorBanner.ShouldBe(Texts.ErrorTerminalStartFailed);
    }

    [Fact]
    public void The_d_key_starts_the_desktop_and_the_c_key_claude_code()
    {
        Page().OnKeyDown(Key.D, KeyModifiers.None).ShouldBeTrue();
        Page().OnKeyDown(Key.C, KeyModifiers.None).ShouldBeTrue();
    }

    [Fact]
    public void Escape_goes_back_to_the_account_picker()
    {
        var picker = new AccountPickerViewModel(_services.Services);
        _services.Navigator.Reset(picker);
        var page = Page();
        _services.Navigator.NavigateTo(page);

        page.OnKeyDown(Key.Escape, KeyModifiers.None).ShouldBeTrue();

        _services.Navigator.CurrentPage.ShouldBe(picker);
    }

    [Fact]
    public void The_last_used_target_is_preferred_for_the_focus()
    {
        _services.Accounts.UpdateSettings(settings => settings with { LastTarget = LaunchTarget.Code });

        Page().PreferredTarget.ShouldBe(LaunchTarget.Code);
    }

    [Fact]
    public void Without_a_last_used_target_the_desktop_is_preferred()
    {
        Page().PreferredTarget.ShouldBe(LaunchTarget.Desktop);
    }
}
