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
    /// <summary>Layout rounding snaps a width to whole pixels.</summary>
    private const double PixelTolerance = 1;

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

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(94)]
    [InlineData(100)]
    public void A_usage_bar_fills_its_track_as_far_as_the_value(double value)
    {
        var bar = new UsageBar { Label = "5h", Value = value, Width = 200 };

        var window = Show(bar);

        FillWidth(bar).ShouldBe(TrackWidth(bar) * value / 100, PixelTolerance);
    }

    [AvaloniaFact]
    public void A_usage_bar_without_a_value_leaves_its_track_empty()
    {
        var bar = new UsageBar { Label = "Woche", Value = null, Width = 200 };

        var window = Show(bar);

        FillWidth(bar).ShouldBe(0, PixelTolerance);
    }

    [AvaloniaFact]
    public void A_usage_bar_follows_a_changed_value()
    {
        var bar = new UsageBar { Label = "5h", Value = 10, Width = 200 };
        var window = Show(bar);

        bar.Value = 80;
        window.UpdateLayout();

        FillWidth(bar).ShouldBe(TrackWidth(bar) * 0.8, PixelTolerance);
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

    private static Border Fill(UsageBar bar) => bar.GetVisualDescendants()
        .OfType<Border>()
        .Single(border => border.Name == "PART_Fill");

    private static IBrush? FillBrush(UsageBar bar) => Fill(bar).Background;

    private static double FillWidth(UsageBar bar) => Fill(bar).Bounds.Width;

    /// <summary>The inner width of the track, which the fill spans at 100 %.</summary>
    private static double TrackWidth(UsageBar bar) => ((Visual)Fill(bar).GetVisualParent()!).Bounds.Width;

    private static IBrush Brush(string key) =>
        (IBrush)Application.Current!.FindResource(Application.Current!.ActualThemeVariant, key)!;

    [AvaloniaFact]
    public void An_icon_without_a_kind_draws_nothing()
    {
        new MIcon().Text.ShouldBeNullOrEmpty();
    }

    [AvaloniaTheory]
    [InlineData(300, 1)]
    [InlineData(640, 2)]
    [InlineData(1000, 3)]
    public void A_tile_grid_fits_as_many_columns_as_its_width_allows(double width, int columns)
    {
        var grid = new TileGrid { MinColumnWidth = 300, Spacing = 12, Width = width };
        for (var i = 0; i < 4; i++)
        {
            grid.Children.Add(new Border { Height = 50 });
        }

        var window = Show(grid);
        window.Width = width + 100;
        window.UpdateLayout();

        var expected = (width - (columns - 1) * 12) / columns;
        grid.Children.ShouldAllBe(child => Math.Abs(child.Bounds.Width - expected) <= PixelTolerance);
        grid.Children.Select(child => child.Bounds.Y).Distinct().Count().ShouldBe((4 + columns - 1) / columns);
    }

    [AvaloniaFact]
    public void A_tile_grid_gives_every_tile_the_height_of_the_tallest()
    {
        var grid = new TileGrid { MinColumnWidth = 100, Spacing = 10, Width = 210 };
        grid.Children.Add(new Border { MinHeight = 40 });
        grid.Children.Add(new Border { MinHeight = 90 });
        grid.Children.Add(new Border { MinHeight = 30 });

        Show(grid);

        grid.Children.ShouldAllBe(child => child.Bounds.Height == 90);
        grid.Children[2].Bounds.Y.ShouldBe(100);
        grid.Children[2].Bounds.Width.ShouldBe(100);
    }

    [AvaloniaFact]
    public void A_tile_grid_narrower_than_one_column_keeps_one_full_width_column()
    {
        var grid = new TileGrid { MinColumnWidth = 300, Spacing = 12, Width = 200 };
        grid.Children.Add(new Border { Height = 50 });

        Show(grid);

        grid.Children[0].Bounds.Width.ShouldBe(200);
    }
}
