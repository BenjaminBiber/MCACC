using Avalonia.Input;
using Mcacc.App.Localization;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

public sealed class AccountPickerViewModelTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TestServices _services = new(OsKind.Windows, Standard, Privat);

    public void Dispose() => _services.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AccountPickerViewModel Picker() => new(_services.Services);

    [Fact]
    public void The_page_lists_every_account_in_order()
    {
        Picker().Tiles.Select(tile => tile.Id).ShouldBe(["standard", "privat"]);
    }

    [Fact]
    public void The_entries_are_the_tiles_followed_by_the_add_tile()
    {
        var picker = Picker();

        picker.Entries.ShouldBe([picker.Tiles[0], picker.Tiles[1], picker.AddTile]);
        picker.AddTile.Title.ShouldBe(Texts.AddAccount);
        picker.AddTile.Command.ShouldBeSameAs(picker.AddCommand);
    }

    [Fact]
    public void A_tile_shows_the_login_email_or_the_hint()
    {
        _services.SetLogin(Privat, "privat@example.com");

        var tiles = Picker().Tiles;

        tiles.Single(tile => tile.Id == "privat").Subtitle.ShouldBe("privat@example.com");
        tiles.Single(tile => tile.Id == "standard").Subtitle.ShouldBe(Texts.NotLoggedIn);
    }

    [Theory]
    [InlineData("Privat", "P")]
    [InlineData("Kunde Müller", "KM")]
    [InlineData("a b c", "AB")]
    [InlineData("  ", "?")]
    public void Initials_take_the_first_letters_of_up_to_two_words(string name, string expected)
    {
        AccountTileViewModel.InitialsOf(name).ShouldBe(expected);
    }

    [Fact]
    public void A_tile_starts_with_the_cached_limits()
    {
        var snapshot = new UsageSnapshot(new UsageWindow(42, null), new UsageWindow(7, null), DateTimeOffset.UnixEpoch);
        _services.Usage.Cached["privat"] = new AccountUsage(UsageState.Ok, snapshot);

        var tile = Picker().Tiles.Single(t => t.Id == "privat");

        tile.FiveHour.Value.ShouldBe(42);
        tile.SevenDay.Value.ShouldBe(7);
        tile.UsageHint.ShouldBeNull();
    }

    [Fact]
    public async Task Activating_refreshes_the_limits_of_every_account()
    {
        var fresh = new UsageSnapshot(new UsageWindow(91, null), null, DateTimeOffset.UnixEpoch);
        _services.Usage.Fresh["privat"] = new AccountUsage(UsageState.Ok, fresh);
        var picker = Picker();

        await picker.ActivateAsync(Ct);

        _services.Usage.Refreshed.Order().ShouldBe(["privat", "standard"]);
        picker.Tiles.Single(t => t.Id == "privat").FiveHour.Value.ShouldBe(91);
    }

    [Fact]
    public async Task An_expired_token_shows_the_hint_to_start_claude_code()
    {
        _services.Usage.Fresh["privat"] = new AccountUsage(UsageState.Expired, null);
        var picker = Picker();

        await picker.ActivateAsync(Ct);

        picker.Tiles.Single(t => t.Id == "privat").UsageHint.ShouldBe(Texts.UsageExpired);
    }

    [Fact]
    public async Task A_rate_limited_refresh_shows_the_time_of_the_cached_value()
    {
        var fetchedAt = new DateTimeOffset(2026, 9, 23, 14, 5, 0, TimeSpan.Zero);
        var snapshot = new UsageSnapshot(new UsageWindow(42, null), null, fetchedAt);
        _services.Usage.Fresh["privat"] = new AccountUsage(UsageState.RateLimited, snapshot);
        var picker = Picker();

        await picker.ActivateAsync(Ct);

        picker.Tiles.Single(t => t.Id == "privat").UsageHint.ShouldBe(Texts.UsageAsOf(fetchedAt));
    }

    [Fact]
    public async Task A_token_of_another_account_shows_its_email_and_no_limits()
    {
        var snapshot = new UsageSnapshot(new UsageWindow(42, null), new UsageWindow(7, null), DateTimeOffset.UnixEpoch);
        _services.Usage.Cached["privat"] = new AccountUsage(UsageState.Ok, snapshot);
        _services.Usage.Fresh["privat"] = new AccountUsage(UsageState.WrongAccount, null, "privat@example.com");
        var picker = Picker();

        await picker.ActivateAsync(Ct);

        var tile = picker.Tiles.Single(t => t.Id == "privat");
        tile.UsageHint.ShouldBe(Texts.UsageWrongAccount("privat@example.com"));
        tile.UsageHint.ShouldNotBeNull().ShouldContain("privat@example.com");
        tile.FiveHour.Value.ShouldBeNull();
        tile.SevenDay.Value.ShouldBeNull();
    }

    [Fact]
    public void The_wrong_account_hint_reads_without_an_email()
    {
        Texts.UsageWrongAccount(null).ShouldNotContain("{");
        Texts.UsageWrongAccount(null).ShouldNotBe(Texts.UsageWrongAccount("privat@example.com"));
    }

    [Fact]
    public async Task A_not_logged_in_account_shows_no_usage_hint()
    {
        _services.Usage.Fresh["privat"] = new AccountUsage(UsageState.NotLoggedIn, null);
        var picker = Picker();

        await picker.ActivateAsync(Ct);

        picker.Tiles.Single(t => t.Id == "privat").UsageHint.ShouldBeNull();
    }

    [Fact]
    public async Task A_failing_refresh_is_swallowed_and_shows_no_dialog()
    {
        _services.Usage.ThrowOnRefresh = new InvalidOperationException("boom");
        var picker = Picker();

        await picker.ActivateAsync(Ct);

        _services.Dialogs.Shown.ShouldBeEmpty();
        _services.Window.ErrorBanner.ShouldBeNull();
    }

    [Fact]
    public async Task The_running_account_gets_the_badge()
    {
        _services.Detector.Instances.Add(_services.RunningInstance(Privat));
        var picker = Picker();

        await picker.ActivateAsync(Ct);

        picker.Tiles.Single(t => t.Id == "privat").Badge.ShouldBe(Texts.RunningBadge);
        picker.Tiles.Single(t => t.Id == "standard").Badge.ShouldBeNull();
    }

    [Fact]
    public async Task A_failing_detection_leaves_every_badge_off()
    {
        _services.Detector.ThrowOnDetect = new TimeoutException("WMI hangs.");
        var picker = Picker();

        await picker.ActivateAsync(Ct);

        picker.Tiles.ShouldAllBe(tile => tile.Badge == null);
    }

    [Fact]
    public void Selecting_an_account_opens_the_target_picker()
    {
        var picker = Picker();

        picker.SelectCommand.Execute(picker.Tiles[1]);

        var page = _services.Navigator.CurrentPage.ShouldBeOfType<TargetPickerViewModel>();
        page.Account.ShouldBe(Privat);
    }

    [Fact]
    public void The_number_keys_select_the_nth_account()
    {
        var picker = Picker();

        picker.OnKeyDown(Key.D2, KeyModifiers.None).ShouldBeTrue();

        _services.Navigator.CurrentPage.ShouldBeOfType<TargetPickerViewModel>().Account.ShouldBe(Privat);
    }

    [Fact]
    public void A_number_key_beyond_the_list_does_nothing()
    {
        var picker = Picker();

        picker.OnKeyDown(Key.D9, KeyModifiers.None).ShouldBeTrue();

        _services.Navigator.CurrentPage.ShouldBeNull();
    }

    [Fact]
    public void Escape_closes_the_app()
    {
        Picker().OnKeyDown(Key.Escape, KeyModifiers.None).ShouldBeTrue();

        _services.Lifetime.Shutdowns.ShouldBe(1);
    }

    [Fact]
    public async Task F2_edits_the_focused_account()
    {
        _services.Dialogs.Answer<AccountEditorViewModel>(editor =>
        {
            editor.Name = "Privat neu";
            editor.SubmitCommand.Execute(null);
            return editor.Completion.Result;
        });
        var picker = Picker();

        picker.OnKeyDown(Key.F2, KeyModifiers.None, picker.Tiles[1]).ShouldBeTrue();
        await Task.Yield();

        _services.Accounts.Updated.ShouldBe([("privat", "Privat neu", Privat.Color)]);
    }

    [Fact]
    public async Task Delete_removes_the_focused_account()
    {
        _services.Dialogs.Answer<RemoveAccountDialogViewModel>(_ => new RemoveAccountResult(DeleteData: false));
        var picker = Picker();

        picker.OnKeyDown(Key.Delete, KeyModifiers.None, picker.Tiles[1]).ShouldBeTrue();
        await Task.Yield();

        _services.Accounts.Removed.ShouldBe([("privat", false)]);
    }

    [Fact]
    public void Delete_leaves_the_default_account_alone()
    {
        var picker = Picker();

        picker.OnKeyDown(Key.Delete, KeyModifiers.None, picker.Tiles[0]).ShouldBeFalse();

        _services.Dialogs.Shown.ShouldBeEmpty();
    }

    [Fact]
    public void F2_without_a_focused_tile_does_nothing()
    {
        Picker().OnKeyDown(Key.F2, KeyModifiers.None, focused: null).ShouldBeFalse();
    }

    [Fact]
    public void Ctrl_comma_opens_the_settings()
    {
        Picker().OnKeyDown(Key.OemComma, KeyModifiers.Control).ShouldBeTrue();

        _services.Navigator.CurrentPage.ShouldBeOfType<SettingsViewModel>();
    }

    [Fact]
    public void The_last_used_account_is_preferred_for_the_focus()
    {
        _services.Accounts.UpdateSettings(settings => settings with { LastAccountId = "privat" });

        Picker().PreferredTile!.Id.ShouldBe("privat");
    }

    [Fact]
    public void Without_a_last_used_account_the_first_tile_is_preferred()
    {
        Picker().PreferredTile!.Id.ShouldBe("standard");
    }

    [Fact]
    public void Opening_the_folder_of_a_profile_reveals_its_profile_directory()
    {
        var picker = Picker();

        picker.OpenFolderCommand.Execute(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Integration.Revealed.ShouldBe([_services.Paths.ProfileDir("privat")]);
    }

    [Fact]
    public void Opening_the_folder_of_the_default_account_reveals_its_claude_directory()
    {
        var picker = Picker();

        picker.OpenFolderCommand.Execute(picker.Tiles.Single(t => t.Id == "standard"));

        _services.Integration.Revealed.ShouldBe([_services.Resolver.Resolve(Standard).EffectiveClaudeDir]);
    }

    [Fact]
    public void A_failing_reveal_does_not_show_an_error()
    {
        _services.Integration.ThrowOnReveal = new InvalidOperationException("no explorer");
        var picker = Picker();

        picker.OpenFolderCommand.Execute(picker.Tiles[0]);

        _services.Window.ErrorBanner.ShouldBeNull();
    }

    [Fact]
    public async Task Removing_an_account_without_data_takes_it_off_the_list()
    {
        _services.Dialogs.Answer<RemoveAccountDialogViewModel>(_ => new RemoveAccountResult(DeleteData: false));
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Accounts.Removed.ShouldBe([("privat", false)]);
        picker.Tiles.Select(tile => tile.Id).ShouldBe(["standard"]);
        picker.Entries.ShouldBe([picker.Tiles[0], picker.AddTile]);
    }

    [Fact]
    public async Task Cancelling_the_remove_dialog_removes_nothing()
    {
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Dialogs.Shown.ShouldHaveSingleItem().ShouldBeOfType<RemoveAccountDialogViewModel>();
        _services.Accounts.Removed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_the_data_is_blocked_while_claude_runs_with_that_account()
    {
        _services.Detector.Instances.Add(_services.RunningInstance(Privat));
        _services.Dialogs.Answer<RemoveAccountDialogViewModel>(_ => new RemoveAccountResult(DeleteData: true));
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Accounts.Removed.ShouldBeEmpty();
        _services.Window.ErrorBanner.ShouldBe(Texts.ErrorRunningWithThisAccount);
    }

    [Fact]
    public async Task Deleting_the_data_works_when_nothing_runs()
    {
        _services.Dialogs.Answer<RemoveAccountDialogViewModel>(_ => new RemoveAccountResult(DeleteData: true));
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Accounts.Removed.ShouldBe([("privat", true)]);
    }

    [Fact]
    public async Task A_failing_removal_shows_an_error_and_keeps_the_account()
    {
        _services.Accounts.ThrowOnRemove = new IOException("the folder is in use");
        _services.Dialogs.Answer<RemoveAccountDialogViewModel>(_ => new RemoveAccountResult(DeleteData: true));
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Window.ErrorBanner.ShouldBe("the folder is in use");
        picker.Tiles.Select(tile => tile.Id).ShouldBe(["standard", "privat"]);
    }

    [Fact]
    public async Task The_default_account_is_never_removed()
    {
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "standard"));

        _services.Dialogs.Shown.ShouldBeEmpty();
        _services.Accounts.Removed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Adding_an_account_puts_a_new_tile_on_the_page()
    {
        _services.Dialogs.Answer<AccountEditorViewModel>(editor =>
        {
            editor.Name = "Arbeit";
            editor.SubmitCommand.Execute(null);
            return editor.Completion.Result;
        });
        var picker = Picker();

        await picker.AddCommand.ExecuteAsync(null);

        picker.Tiles.Select(tile => tile.Name).ShouldBe(["Standard", "Privat", "Arbeit"]);
        picker.Entries.ShouldBe([.. picker.Tiles, picker.AddTile]);
    }

    [Fact]
    public async Task A_cancelled_editor_changes_nothing()
    {
        var picker = Picker();

        await picker.AddCommand.ExecuteAsync(null);

        _services.Accounts.Created.ShouldBeEmpty();
        picker.Tiles.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Editing_an_account_renames_its_tile()
    {
        _services.Dialogs.Answer<AccountEditorViewModel>(editor =>
        {
            editor.Name = "Privat neu";
            editor.SubmitCommand.Execute(null);
            return editor.Completion.Result;
        });
        var picker = Picker();

        await picker.EditCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Accounts.Updated.ShouldBe([("privat", "Privat neu", Privat.Color)]);
        picker.Tiles.Single(t => t.Id == "privat").Name.ShouldBe("Privat neu");
    }

    [Fact]
    public async Task A_poll_updates_the_tile_of_that_account()
    {
        var picker = Picker();
        await picker.ActivateAsync(Ct);
        var snapshot = new UsageSnapshot(new UsageWindow(77, null), null, DateTimeOffset.Now);
        _services.Usage.Fresh["privat"] = new AccountUsage(UsageState.Ok, snapshot);

        await _services.Poller.TickAsync(Ct);

        picker.Tiles.Single(tile => tile.Id == "privat").FiveHour.Value.ShouldBe(77);
    }

    [Fact]
    public async Task A_deactivated_picker_ignores_polls()
    {
        var picker = Picker();
        await picker.ActivateAsync(Ct);
        picker.Deactivate();
        _services.Usage.Fresh["privat"] =
            new AccountUsage(UsageState.Ok, new UsageSnapshot(new UsageWindow(77, null), null, DateTimeOffset.Now));

        await _services.Poller.TickAsync(Ct);

        picker.Tiles.Single(tile => tile.Id == "privat").FiveHour.Value.ShouldBeNull();
    }

    [Fact]
    public async Task Removing_an_account_forgets_its_limits_and_statistics()
    {
        _services.Dialogs.Answer<RemoveAccountDialogViewModel>(_ => new RemoveAccountResult(DeleteData: false));
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Usage.Forgotten.ShouldBe(["privat"]);
        _services.Stats.Forgotten.ShouldBe(["privat"]);
    }

    [Fact]
    public async Task A_failing_removal_forgets_nothing()
    {
        _services.Accounts.ThrowOnRemove = new IOException("the folder is in use");
        _services.Dialogs.Answer<RemoveAccountDialogViewModel>(_ => new RemoveAccountResult(DeleteData: false));
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Usage.Forgotten.ShouldBeEmpty();
        _services.Stats.Forgotten.ShouldBeEmpty();
    }

    [Fact]
    public void The_info_entry_opens_the_info_page_of_that_account()
    {
        var picker = Picker();
        _services.Navigator.Reset(picker);

        picker.OpenInfoCommand.Execute(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Navigator.CurrentPage.ShouldBeOfType<AccountInfoViewModel>().Account.Id.ShouldBe("privat");
        _services.Navigator.CanGoBack.ShouldBeTrue();
    }
}
