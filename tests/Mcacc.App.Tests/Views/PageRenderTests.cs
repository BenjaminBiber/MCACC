using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Mcacc.App.Controls;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.App.Views;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Views;

/// <summary>Renders the real pages headless, so a broken binding or template fails a test instead of the app.</summary>
public sealed class PageRenderTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TestServices _services = new(OsKind.Windows, Standard, Privat);

    public void Dispose() => _services.Dispose();

    private Window Show(PageViewModel page)
    {
        _services.Navigator.Reset(page);
        var window = new MainWindow { DataContext = _services.Window, Width = 760, Height = 540 };
        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void The_account_picker_renders_a_tile_per_account_and_the_add_tile()
    {
        _services.SetLogin(Privat, "privat@example.com");

        var window = Show(new AccountPickerViewModel(_services.Services));

        var tiles = window.GetVisualDescendants().OfType<LaunchTile>().ToList();
        tiles.Count(tile => tile.Variant == LaunchTileVariant.Account).ShouldBe(2);
        tiles.Count(tile => tile.Variant == LaunchTileVariant.Add).ShouldBe(1);
        tiles.Select(tile => tile.Title).ShouldContain("Privat");
        tiles.Single(tile => tile.Title == "Privat").Subtitle.ShouldBe("privat@example.com");
    }

    [AvaloniaFact]
    public void The_account_picker_renders_a_usage_bar_pair_per_account()
    {
        var window = Show(new AccountPickerViewModel(_services.Services));

        var bars = window.GetVisualDescendants().OfType<UsageBar>().ToList();
        bars.Count.ShouldBe(4);
        bars.Select(bar => bar.Label).Distinct().Order().ShouldBe(["5h", "Woche"]);
    }

    [AvaloniaFact]
    public void The_target_picker_renders_both_target_tiles()
    {
        var window = Show(new TargetPickerViewModel(_services.Services, Privat));

        var tiles = window.GetVisualDescendants().OfType<LaunchTile>().ToList();
        tiles.Count.ShouldBe(2);
        tiles.Select(tile => tile.Title).ShouldBe(["Claude Desktop", "Claude Code"]);
        tiles.ShouldAllBe(tile => tile.Variant == LaunchTileVariant.Target);
    }

    [AvaloniaFact]
    public void The_settings_page_renders_its_controls()
    {
        var window = Show(new SettingsViewModel(_services.Services));

        window.GetVisualDescendants().OfType<ComboBox>().ShouldNotBeEmpty();
        window.GetVisualDescendants().OfType<ToggleSwitch>().ShouldHaveSingleItem();
    }

    [AvaloniaFact]
    public void The_error_banner_appears_only_with_a_message()
    {
        var window = Show(new AccountPickerViewModel(_services.Services));
        Banner(window).ShouldBeNull();

        _services.Window.ShowError("Da ging was schief.");

        Banner(window).ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void The_dialog_overlay_appears_with_a_dialog()
    {
        var window = Show(new AccountPickerViewModel(_services.Services));
        var overlay = window.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == "PART_DialogLayer");
        overlay.IsVisible.ShouldBeFalse();

        _services.Window.Dialog = new ConfirmDialogViewModel("Titel", "Text", "Ja");
        window.UpdateLayout();

        overlay.IsVisible.ShouldBeTrue();
        window.GetVisualDescendants().OfType<ConfirmDialogView>().ShouldHaveSingleItem();
    }

    [AvaloniaFact]
    public void The_busy_overlay_appears_while_busy()
    {
        var window = Show(new AccountPickerViewModel(_services.Services));
        var overlay = window.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == "PART_BusyLayer");
        overlay.IsVisible.ShouldBeFalse();

        _services.Window.IsBusy = true;

        overlay.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_remove_dialog_renders_its_folder_list()
    {
        var window = Show(new AccountPickerViewModel(_services.Services));

        _services.Window.Dialog = new RemoveAccountDialogViewModel(_services.Services, Privat);
        window.UpdateLayout();

        var view = window.GetVisualDescendants().OfType<RemoveAccountDialogView>().ShouldHaveSingleItem();
        view.GetVisualDescendants().OfType<CheckBox>().ShouldHaveSingleItem();
    }

    [AvaloniaFact]
    public void The_account_editor_renders_a_swatch_per_palette_color()
    {
        var window = Show(new AccountPickerViewModel(_services.Services));

        _services.Window.Dialog = AccountEditorViewModel.ForCreate(_services.Services);
        window.UpdateLayout();

        var view = window.GetVisualDescendants().OfType<AccountEditorView>().ShouldHaveSingleItem();
        view.GetVisualDescendants().OfType<Button>().Count(button => button.Width == 28)
            .ShouldBe(AccountPalette.Colors.Count);
    }

    [AvaloniaFact]
    public void The_tile_menu_shows_its_entries_and_runs_their_commands()
    {
        var picker = new AccountPickerViewModel(_services.Services);
        var window = Show(picker);
        var privat = window.GetVisualDescendants().OfType<LaunchTile>().Single(tile => tile.Title == "Privat");
        var more = privat.GetVisualDescendants().OfType<Button>().Single(button => button.Flyout is not null);

        var flyout = more.Flyout.ShouldBeOfType<MenuFlyout>();
        flyout.ShowAt(more);
        window.UpdateLayout();

        var items = flyout.Items.OfType<MenuItem>().ToList();
        items.Select(item => item.Header).ShouldBe([picker.EditText, picker.RemoveText, picker.OpenFolderText]);
        items.ShouldAllBe(item => item.Command != null && ReferenceEquals(item.CommandParameter, privat.DataContext));
    }

    private static Border? Banner(Window window) =>
        window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("banner") && b.IsVisible);
}
