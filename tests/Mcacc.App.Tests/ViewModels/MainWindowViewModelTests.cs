using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TestServices _services = new(OsKind.Windows, Account.CreateDefault());

    public void Dispose() => _services.Dispose();

    private MainWindowViewModel Shell => _services.Window;

    private AccountPickerViewModel Picker() => new(_services.Services);

    [Fact]
    public void Hiding_takes_the_banner_down()
    {
        Shell.ShowError("Fehler");

        Shell.OnHidden();

        Shell.ErrorBanner.ShouldBeNull();
    }

    [Fact]
    public async Task Hiding_cancels_an_open_dialog()
    {
        var dialog = new ConfirmDialogViewModel("Titel", "Text", "OK");
        Shell.Dialog = dialog;

        Shell.OnHidden();

        dialog.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        (await dialog.Completion).ShouldNotBe(true);
    }

    [Fact]
    public void After_a_hide_the_window_opens_on_the_start_page()
    {
        _services.Navigator.Reset(new SettingsViewModel(_services.Services));
        Shell.OnHidden();

        Shell.OnShown(Picker);

        Shell.Navigator.CurrentPage.ShouldBeOfType<AccountPickerViewModel>();
        Shell.Navigator.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Showing_a_window_that_was_not_hidden_keeps_its_page()
    {
        var settings = new SettingsViewModel(_services.Services);
        _services.Navigator.Reset(settings);

        Shell.OnShown(Picker);

        Shell.Navigator.CurrentPage.ShouldBeSameAs(settings);
    }

    [Fact]
    public void A_start_under_way_keeps_its_page_until_the_next_show()
    {
        var settings = new SettingsViewModel(_services.Services);
        _services.Navigator.Reset(settings);
        Shell.OnHidden();
        Shell.IsBusy = true;

        Shell.OnShown(Picker);
        Shell.Navigator.CurrentPage.ShouldBeSameAs(settings);

        Shell.IsBusy = false;
        Shell.OnShown(Picker);
        Shell.Navigator.CurrentPage.ShouldBeOfType<AccountPickerViewModel>();
    }

    [Fact]
    public void A_start_at_login_opens_a_fresh_start_page_on_the_first_show()
    {
        var settings = new SettingsViewModel(_services.Services);
        _services.Navigator.Reset(settings);

        Shell.OnStartedHidden();
        Shell.OnShown(Picker);

        Shell.Navigator.CurrentPage.ShouldBeOfType<AccountPickerViewModel>();
        Shell.Navigator.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void A_start_at_login_keeps_the_banner_of_the_start()
    {
        Shell.ShowError("x");

        Shell.OnStartedHidden();
        Shell.OnShown(Picker);

        Shell.ErrorBanner.ShouldBe("x");
    }
}
