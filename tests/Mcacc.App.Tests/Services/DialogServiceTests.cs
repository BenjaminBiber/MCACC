using Avalonia.Input;
using Mcacc.App.Services;
using Mcacc.App.ViewModels;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Services;

public class DialogServiceTests
{
    private sealed class YesNoDialog : DialogViewModel<bool>
    {
        public override string Title => "Frage";
    }

    private static (MainWindowViewModel Owner, DialogService Service) Create()
    {
        var owner = new MainWindowViewModel(new Navigator());
        return (owner, new DialogService(owner));
    }

    [Fact]
    public async Task Showing_a_dialog_sets_the_overlay_and_completes_with_the_result()
    {
        var (owner, service) = Create();
        var dialog = new YesNoDialog();

        var pending = service.ShowAsync(dialog);
        owner.Dialog.ShouldBe(dialog);

        dialog.Complete(true);

        (await pending).ShouldBeTrue();
        owner.Dialog.ShouldBeNull();
    }

    [Fact]
    public async Task Cancelling_completes_with_the_default_and_clears_the_overlay()
    {
        var (owner, service) = Create();
        var dialog = new YesNoDialog();

        var pending = service.ShowAsync(dialog);
        dialog.Cancel();

        (await pending).ShouldBeFalse();
        owner.Dialog.ShouldBeNull();
    }

    [Fact]
    public async Task Only_the_first_completion_counts()
    {
        var (_, service) = Create();
        var dialog = new YesNoDialog();

        var pending = service.ShowAsync(dialog);
        dialog.Complete(true);
        dialog.Complete(false);

        (await pending).ShouldBeTrue();
    }

    [Fact]
    public async Task Escape_cancels_the_open_dialog()
    {
        var (owner, service) = Create();
        var dialog = new YesNoDialog();
        var pending = service.ShowAsync(dialog);

        owner.OnKeyDown(Key.Escape, KeyModifiers.None).ShouldBeTrue();

        (await pending).ShouldBeFalse();
    }

    [Fact]
    public void A_busy_scope_shows_and_hides_the_busy_overlay()
    {
        var (owner, service) = Create();

        using (service.Busy("Claude wird gestartet…"))
        {
            owner.IsBusy.ShouldBeTrue();
            owner.BusyText.ShouldBe("Claude wird gestartet…");
        }

        owner.IsBusy.ShouldBeFalse();
        owner.BusyText.ShouldBeNull();
    }

    [Fact]
    public void While_busy_no_key_reaches_the_page()
    {
        var (owner, service) = Create();

        using var busy = service.Busy("…");

        owner.OnKeyDown(Key.D, KeyModifiers.None).ShouldBeTrue();
    }

    [Fact]
    public void The_error_banner_is_shown_and_dismissed()
    {
        var (owner, _) = Create();

        owner.ShowError("Da ging was schief.");
        owner.ErrorBanner.ShouldBe("Da ging was schief.");

        owner.DismissErrorCommand.Execute(null);

        owner.ErrorBanner.ShouldBeNull();
    }
}
