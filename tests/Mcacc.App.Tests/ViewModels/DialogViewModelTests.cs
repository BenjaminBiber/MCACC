using Mcacc.App.Localization;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

public sealed class DialogViewModelTests : IDisposable
{
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TestServices _services = new(OsKind.Windows, Account.CreateDefault(), Privat);

    public void Dispose() => _services.Dispose();

    private RemoveAccountDialogViewModel RemoveDialog() => new(_services.Services, Privat);

    [Fact]
    public async Task A_confirm_dialog_completes_with_yes()
    {
        var dialog = new ConfirmDialogViewModel("Titel", "Text", "Ja");

        dialog.ConfirmCommand.Execute(null);

        (await dialog.Completion).ShouldBeTrue();
    }

    [Fact]
    public async Task A_confirm_dialog_completes_with_no()
    {
        var dialog = new ConfirmDialogViewModel("Titel", "Text", "Ja");

        dialog.RejectCommand.Execute(null);

        (await dialog.Completion).ShouldBeFalse();
    }

    [Fact]
    public void A_confirm_dialog_carries_its_texts()
    {
        var dialog = new ConfirmDialogViewModel(Texts.ConflictDialogTitle(ProviderKind.Claude), "Text", Texts.ConflictDialogRestart);

        dialog.Title.ShouldBe(Texts.ConflictDialogTitle(ProviderKind.Claude));
        dialog.ConfirmText.ShouldBe(Texts.ConflictDialogRestart);
        dialog.CancelText.ShouldBe(Texts.Cancel);
        dialog.IsDangerous.ShouldBeFalse();
    }

    [Fact]
    public void The_remove_dialog_names_the_account_and_lists_both_folders()
    {
        var dialog = RemoveDialog();

        dialog.Title.ShouldBe(Texts.RemoveDialogTitle("Privat"));
        dialog.Folders.ShouldBe([
            _services.Paths.DesktopDataDir("privat", ProviderDescriptors.Claude),
            _services.Paths.ConfigDir("privat", ProviderDescriptors.Claude),
        ]);
    }

    [Fact]
    public void The_remove_dialog_starts_without_deleting_data()
    {
        RemoveDialog().DeleteData.ShouldBeFalse();
    }

    [Fact]
    public async Task The_remove_dialog_reports_the_checkbox()
    {
        var dialog = RemoveDialog();
        dialog.DeleteData = true;

        dialog.ConfirmCommand.Execute(null);

        (await dialog.Completion)!.DeleteData.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancelling_the_remove_dialog_yields_no_result()
    {
        var dialog = RemoveDialog();
        dialog.DeleteData = true;

        dialog.RejectCommand.Execute(null);

        (await dialog.Completion).ShouldBeNull();
    }
}
