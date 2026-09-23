using Mcacc.App.Localization;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

public sealed class AccountEditorViewModelTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TestServices _services = new(OsKind.Windows, Standard, Privat);

    public void Dispose() => _services.Dispose();

    private AccountEditorViewModel Create() => AccountEditorViewModel.ForCreate(_services.Services);

    private AccountEditorViewModel Edit(Account account) => AccountEditorViewModel.ForEdit(_services.Services, account);

    /// <summary>Puts the files of an item into the source account's claude directory.</summary>
    private void GiveSourceItem(Account source, string relativePath, bool isDirectory = false)
    {
        var claudeDir = _services.Resolver.Resolve(source).EffectiveClaudeDir;
        var path = Path.Join(claudeDir, relativePath);
        if (isDirectory)
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(claudeDir);
            File.WriteAllText(path, "");
        }
    }

    [Fact]
    public void A_new_account_starts_with_the_next_free_color()
    {
        Create().SelectedColor.ShouldBe(AccountPalette.Colors.First(color =>
            color != Standard.Color && color != Privat.Color));
    }

    [Fact]
    public void The_editor_titles_differ_between_creating_and_editing()
    {
        Create().Title.ShouldBe(Texts.EditorCreateTitle);
        Edit(Privat).Title.ShouldBe(Texts.EditorEditTitle);
    }

    [Fact]
    public void An_empty_name_is_refused()
    {
        var editor = Create();
        editor.Name = "   ";

        editor.SubmitCommand.Execute(null);

        editor.NameError.ShouldBe(Texts.EditorNameEmpty);
        _services.Accounts.Created.ShouldBeEmpty();
    }

    [Fact]
    public void A_name_longer_than_forty_characters_is_refused()
    {
        var editor = Create();
        editor.Name = new string('x', 41);

        editor.SubmitCommand.Execute(null);

        editor.NameError.ShouldBe(Texts.EditorNameTooLong);
    }

    [Fact]
    public void A_name_that_already_exists_is_refused()
    {
        var editor = Create();
        editor.Name = "privat";

        editor.SubmitCommand.Execute(null);

        editor.NameError.ShouldBe(Texts.EditorNameDuplicate);
    }

    [Fact]
    public void Keeping_its_own_name_while_editing_is_allowed()
    {
        var editor = Edit(Privat);

        editor.SubmitCommand.Execute(null);

        editor.NameError.ShouldBeNull();
        _services.Accounts.Updated.ShouldBe([("privat", "Privat", Privat.Color)]);
    }

    [Fact]
    public async Task Creating_without_a_copy_source_sends_no_copy_request()
    {
        var editor = Create();
        editor.Name = "Arbeit";

        editor.SubmitCommand.Execute(null);

        _services.Accounts.Created.ShouldBe([("Arbeit", editor.SelectedColor, null)]);
        (await editor.Completion)!.Name.ShouldBe("Arbeit");
    }

    [Fact]
    public void Picking_a_color_swatch_selects_it()
    {
        var editor = Create();

        editor.PickColorCommand.Execute(AccountPalette.Colors[3]);

        editor.SelectedColor.ShouldBe(AccountPalette.Colors[3]);
    }

    [Fact]
    public void Editing_keeps_the_id_and_saves_the_new_color()
    {
        var editor = Edit(Privat);
        editor.Name = "Privat neu";
        editor.PickColorCommand.Execute(AccountPalette.Colors[4]);

        editor.SubmitCommand.Execute(null);

        _services.Accounts.Updated.ShouldBe([("privat", "Privat neu", AccountPalette.Colors[4])]);
    }

    [Fact]
    public void Only_creating_offers_a_copy_source()
    {
        Create().CopySources.Count.ShouldBe(3);
        Edit(Privat).CopySources.ShouldBeEmpty();
    }

    [Fact]
    public void Nothing_is_the_first_copy_source()
    {
        Create().CopySources[0].ShouldBeNull();
    }

    [Fact]
    public void Choosing_a_source_lists_every_copy_item()
    {
        var editor = Create();

        editor.CopySource = Privat;

        editor.CopyItemsToShow.Select(item => item.Item).ShouldBe(Enum.GetValues<CopyItem>());
        editor.HasCopySource.ShouldBeTrue();
    }

    [Fact]
    public void An_item_the_source_does_not_have_is_disabled_and_labelled()
    {
        var editor = Create();

        editor.CopySource = Privat;

        var claudeMd = editor.CopyItemsToShow.Single(item => item.Item == CopyItem.ClaudeMd);
        claudeMd.IsAvailable.ShouldBeFalse();
        claudeMd.IsSelected.ShouldBeFalse();
        claudeMd.Label.ShouldBe(Texts.CopyClaudeMd + Texts.EditorNotAvailableSuffix);
    }

    [Fact]
    public void Available_items_start_checked_except_scripts()
    {
        GiveSourceItem(Privat, "CLAUDE.md");
        GiveSourceItem(Privat, "scripts", isDirectory: true);
        var editor = Create();

        editor.CopySource = Privat;

        editor.CopyItemsToShow.Single(item => item.Item == CopyItem.ClaudeMd).IsSelected.ShouldBeTrue();
        var scripts = editor.CopyItemsToShow.Single(item => item.Item == CopyItem.Scripts);
        scripts.IsAvailable.ShouldBeTrue();
        scripts.IsSelected.ShouldBeFalse();
    }

    [Fact]
    public void Creating_with_a_copy_source_sends_the_checked_items_and_the_rewrite_flag()
    {
        GiveSourceItem(Privat, "CLAUDE.md");
        GiveSourceItem(Privat, "settings.json");
        GiveSourceItem(Privat, "skills", isDirectory: true);
        var editor = Create();
        editor.Name = "Arbeit";
        editor.CopySource = Privat;
        editor.CopyItemsToShow.Single(item => item.Item == CopyItem.Skills).IsSelected = false;

        editor.SubmitCommand.Execute(null);

        var copy = _services.Accounts.Created.ShouldHaveSingleItem().Copy.ShouldNotBeNull();
        copy.SourceAccountId.ShouldBe("privat");
        copy.Items.Order().ShouldBe([CopyItem.ClaudeMd, CopyItem.SettingsJson]);
        copy.RewriteSettingsPaths.ShouldBeTrue();
    }

    [Fact]
    public void Unchecking_the_path_rewrite_reaches_the_request()
    {
        GiveSourceItem(Privat, "settings.json");
        var editor = Create();
        editor.Name = "Arbeit";
        editor.CopySource = Privat;
        editor.RewriteSettingsPaths = false;

        editor.SubmitCommand.Execute(null);

        _services.Accounts.Created.ShouldHaveSingleItem().Copy!.RewriteSettingsPaths.ShouldBeFalse();
    }

    [Fact]
    public void Choosing_a_source_with_nothing_checked_sends_no_request()
    {
        GiveSourceItem(Privat, "CLAUDE.md");
        var editor = Create();
        editor.Name = "Arbeit";
        editor.CopySource = Privat;
        foreach (var item in editor.CopyItemsToShow)
        {
            item.IsSelected = false;
        }

        editor.SubmitCommand.Execute(null);

        _services.Accounts.Created.ShouldHaveSingleItem().Copy.ShouldBeNull();
    }

    [Fact]
    public void Switching_back_to_nothing_clears_the_item_list()
    {
        var editor = Create();
        editor.CopySource = Privat;

        editor.CopySource = null;

        editor.CopyItemsToShow.ShouldBeEmpty();
        editor.HasCopySource.ShouldBeFalse();
    }

    [Fact]
    public void Skipped_copy_entries_are_reported()
    {
        _services.Accounts.CreateReport = new CopyReport(
            [CopyItem.Skills],
            [new SkippedEntry("skills/a/SKILL.md", CopySkipReason.TargetExists)]);
        var editor = Create();
        editor.Name = "Arbeit";

        editor.SubmitCommand.Execute(null);

        editor.SkippedText.ShouldBe(Texts.EditorNotCopied(["skills/a/SKILL.md"]));
    }

    [Fact]
    public void A_failing_create_shows_its_message_and_keeps_the_dialog_open()
    {
        _services.Accounts.ThrowOnCreate = new IOException("the disk is full");
        var editor = Create();
        editor.Name = "Arbeit";

        editor.SubmitCommand.Execute(null);

        editor.SubmitError.ShouldBe("the disk is full");
        editor.Completion.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancelling_completes_without_an_account()
    {
        var editor = Create();

        editor.RejectCommand.Execute(null);

        (await editor.Completion).ShouldBeNull();
    }

    [Fact]
    public void The_submit_button_is_labelled_for_the_mode()
    {
        Create().SubmitText.ShouldBe(Texts.EditorCreate);
        Edit(Privat).SubmitText.ShouldBe(Texts.EditorSave);
    }
}
