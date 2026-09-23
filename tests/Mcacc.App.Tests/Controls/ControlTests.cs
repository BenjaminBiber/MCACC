using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mcacc.App.Controls;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Controls;

public class ControlTests
{
    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 300, Content = content };
        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void A_launch_tile_applies_its_template_and_shows_its_title()
    {
        var tile = new LaunchTile { Title = "Privat", Subtitle = "privat@example.com", Variant = LaunchTileVariant.Account };

        var window = Show(tile);

        tile.IsMeasureValid.ShouldBeTrue();
        var texts = tile.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        texts.ShouldContain("Privat");
        texts.ShouldContain("privat@example.com");
    }

    [AvaloniaFact]
    public void An_account_tile_shows_its_avatar_and_hides_the_target_icon()
    {
        var tile = new LaunchTile
        {
            Title = "Privat",
            AvatarText = "P",
            AvatarBrush = new SolidColorBrush(Color.Parse("#6A9BCC")),
            Variant = LaunchTileVariant.Account,
        };

        var window = Show(tile);

        var avatar = tile.GetVisualDescendants().OfType<Ellipse>().ShouldHaveSingleItem();
        avatar.IsVisible.ShouldBeTrue();
        tile.GetVisualDescendants().OfType<Border>().ShouldNotContain(b => b.Name == "PART_IconBox" && b.IsVisible);
    }

    [AvaloniaFact]
    public void A_target_tile_shows_its_icon_box()
    {
        var tile = new LaunchTile
        {
            Title = "Claude Desktop",
            IconKind = MIconKind.DesktopWindows,
            Variant = LaunchTileVariant.Target,
        };

        var window = Show(tile);

        tile.GetVisualDescendants()
            .OfType<Border>()
            .ShouldContain(b => b.Name == "PART_IconBox" && b.IsVisible);
        tile.GetVisualDescendants().OfType<MIcon>().ShouldHaveSingleItem().Text.ShouldBe(MIconGlyphs.Of(MIconKind.DesktopWindows));
    }

    [AvaloniaFact]
    public void An_add_tile_draws_a_dashed_outline()
    {
        var tile = new LaunchTile { Title = "Account hinzufügen", IconKind = MIconKind.Add, Variant = LaunchTileVariant.Add };

        var window = Show(tile);

        var dash = tile.GetVisualDescendants().OfType<Rectangle>().ShouldHaveSingleItem();
        dash.IsVisible.ShouldBeTrue();
        dash.StrokeDashArray.ShouldBe([4, 3]);
    }

    [AvaloniaFact]
    public void A_tile_names_itself_for_the_accessibility_tree()
    {
        var tile = new LaunchTile { Title = "Privat" };

        Avalonia.Automation.AutomationProperties.GetName(tile).ShouldBe("Privat");
    }

    [AvaloniaFact]
    public void A_tile_without_a_badge_hides_the_badge()
    {
        var tile = new LaunchTile { Title = "Privat", Variant = LaunchTileVariant.Account };

        var window = Show(tile);

        tile.GetVisualDescendants().OfType<Border>().ShouldNotContain(b => b.Classes.Contains("chip") && b.IsVisible);
    }

    [AvaloniaFact]
    public void A_running_tile_shows_the_badge()
    {
        var tile = new LaunchTile { Title = "Privat", Badge = "Läuft", Variant = LaunchTileVariant.Account };

        var window = Show(tile);

        tile.GetVisualDescendants().OfType<Border>().ShouldContain(b => b.Classes.Contains("chip") && b.IsVisible);
    }

    [AvaloniaFact]
    public void A_usage_bar_writes_label_percent_and_reset()
    {
        var bar = new UsageBar { Label = "5h", Value = 42.4, ResetText = "Reset 14:30" };

        var window = Show(bar);

        bar.Text.ShouldBe("5h  42 % · Reset 14:30");
        bar.Fraction.ShouldBe(0.424);
        bar.IsWarning.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_usage_bar_without_a_value_shows_a_dash()
    {
        var bar = new UsageBar { Label = "Woche", Value = null };

        bar.Text.ShouldBe("Woche  —");
        bar.Fraction.ShouldBe(0);
    }

    [AvaloniaTheory]
    [InlineData(89.4, false)]
    [InlineData(90, true)]
    [InlineData(100, true)]
    public void A_usage_bar_warns_from_ninety_percent(double value, bool expected)
    {
        new UsageBar { Label = "5h", Value = value }.IsWarning.ShouldBe(expected);
    }

    [AvaloniaFact]
    public void A_usage_bar_clamps_a_value_above_one_hundred()
    {
        new UsageBar { Label = "5h", Value = 140 }.Fraction.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_usage_bar_below_the_threshold_fills_in_the_accent_color()
    {
        var bar = new UsageBar { Label = "5h", Value = 42 };

        var window = Show(bar);

        FillBrush(bar).ShouldBe(Brush("Mcacc.Accent"));
    }

    [AvaloniaFact]
    public void A_usage_bar_at_the_threshold_fills_in_the_danger_color()
    {
        var bar = new UsageBar { Label = "5h", Value = 94 };

        var window = Show(bar);

        FillBrush(bar).ShouldBe(Brush("Mcacc.Danger"));
    }

    [AvaloniaFact]
    public void A_focused_tile_shows_the_accent_ring_instead_of_the_default_adorner()
    {
        var tile = new LaunchTile { Title = "Privat", Variant = LaunchTileVariant.Account };
        var window = Show(tile);
        // Without the transition the style value is readable right away instead of 120 ms later.
        tile.Transitions = null;

        tile.Focus(NavigationMethod.Tab);
        window.UpdateLayout();

        tile.IsFocused.ShouldBeTrue();

        tile.BorderBrush.ShouldBe(Brush("Mcacc.Accent"));
        tile.BorderThickness.Top.ShouldBe(2);
        tile.FocusAdorner.ShouldBeNull();
    }

    private static IBrush? FillBrush(UsageBar bar) => bar.GetVisualDescendants()
        .OfType<Border>()
        .Single(border => border.Name == "PART_Fill")
        .Background;

    private static IBrush Brush(string key) =>
        (IBrush)Application.Current!.FindResource(Application.Current!.ActualThemeVariant, key)!;

    [AvaloniaFact]
    public void An_icon_without_a_kind_draws_nothing()
    {
        new MIcon().Text.ShouldBeNullOrEmpty();
    }
}
