using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Terminal;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Services;

/// <summary>
/// The launch flow itself is covered through <see cref="TargetPickerViewModel"/>; these are about the window, since a
/// start from the tray begins with the window hidden, and about the limits a start of Claude Code renews.
/// </summary>
public sealed class LaunchCoordinatorTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TestServices _services = new(OsKind.Windows, Standard, Privat);

    public void Dispose() => _services.Dispose();

    private LaunchCoordinator Launcher => new(_services.Services);

    [Fact]
    public async Task A_start_without_trouble_leaves_a_hidden_window_hidden()
    {
        await Launcher.LaunchDesktopAsync(Standard);

        _services.Desktop.Launches.ShouldBe([false]);
        _services.Lifetime.Shows.ShouldBe(0);
        _services.Lifetime.Hides.ShouldBe(1);
    }

    [Fact]
    public async Task Claude_code_without_trouble_leaves_a_hidden_window_hidden()
    {
        await Launcher.LaunchCodeAsync(Privat);

        _services.Code.Launched.ShouldBe([Privat]);
        _services.Lifetime.Shows.ShouldBe(0);
    }

    [Fact]
    public async Task A_conflict_brings_the_window_up_before_it_asks()
    {
        _services.Desktop.Check = new DesktopLaunchCheck.Conflict(
            [new DesktopConflict(_services.RunningInstance(Standard, pid: 200), Standard)]);
        _services.Dialogs.Answer<ConfirmDialogViewModel>(_ => true);

        await Launcher.LaunchDesktopAsync(Privat);

        _services.Lifetime.Shows.ShouldBe(1);
        _services.Dialogs.Shown.ShouldHaveSingleItem().ShouldBeOfType<ConfirmDialogViewModel>();
    }

    [Fact]
    public async Task An_error_brings_the_window_up_so_it_is_seen()
    {
        _services.Desktop.Outcome = DesktopLaunchOutcome.StartFailed;

        await Launcher.LaunchDesktopAsync(Standard);

        _services.Lifetime.Shows.ShouldBe(1);
        _services.Window.ErrorBanner.ShouldBe(Texts.ErrorDesktopStartFailed);
    }

    [Fact]
    public async Task Claude_code_rechecks_the_limits_of_its_account()
    {
        using var services = new TestServices([TimeSpan.Zero], OsKind.Windows, Standard, Privat);

        await new LaunchCoordinator(services.Services).LaunchCodeAsync(Privat);

        services.Usage.Invalidated.ShouldBe(["privat"]);
        services.Usage.Refreshed.ShouldBe(["privat"]);
    }

    [Fact]
    public async Task A_failed_claude_code_start_rechecks_nothing()
    {
        using var services = new TestServices([TimeSpan.Zero], OsKind.Windows, Standard, Privat);
        services.Code.Result = services.Code.Result with { Outcome = CodeLaunchOutcome.StartFailed };

        await new LaunchCoordinator(services.Services).LaunchCodeAsync(Privat);

        services.Usage.Invalidated.ShouldBeEmpty();
    }

    [Fact]
    public async Task Claude_desktop_rechecks_nothing()
    {
        using var services = new TestServices([TimeSpan.Zero], OsKind.Windows, Standard, Privat);

        await new LaunchCoordinator(services.Services).LaunchDesktopAsync(Standard);

        services.Usage.Invalidated.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_login_watch_runs_without_bringing_the_window_up()
    {
        await Launcher.LaunchDesktopAsync(Privat);

        _services.Redirect.Watched.ShouldBe([Privat]);
        _services.Lifetime.Shows.ShouldBe(0);
    }
}
