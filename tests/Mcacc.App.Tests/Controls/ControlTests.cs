using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
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

    private static void Rendered(Window window)
    {
        window.UpdateLayout();
        using var frame = window.CaptureRenderedFrame();
        frame.ShouldNotBeNull();
    }

    private static LineChart TwoPointLine() => new()
    {
        Width = 360,
        Height = 160,
        XMin = 0,
        XMax = 1,
        Threshold = 90,
        StartLabel = "vor 24 h",
        EndLabel = "jetzt",
        XTicks = [new AxisTick(0.5, "12:00"), new AxisTick(0.98, "23:00")],
        Series =
        [
            new LineSeries("5h", ChartColor.Series1, [new ChartPoint(0, 20, "a"), new ChartPoint(1, 95, "b")]),
            new LineSeries("Woche", ChartColor.Series2, [new ChartPoint(0, 5, "a"), new ChartPoint(1, 7, "b")]),
        ],
    };

    [AvaloniaFact]
    public void Every_chart_renders_with_data_and_empty()
    {
        Rendered(Show(TwoPointLine()));
        Rendered(Show(new LineChart { Width = 360, Height = 160, EmptyText = "leer" }));
        Rendered(Show(new StackedBarChart
        {
            Width = 360,
            Height = 160,
            AxisFormatter = value => $"{value:0} k",
            Bars = [new BarStack("1.9.", [new BarSegment(3, ChartColor.Series1), new BarSegment(2, ChartColor.Series2)])],
        }));
        Rendered(Show(new StackedBarChart { Width = 360, Height = 160, EmptyText = "leer" }));
        Rendered(Show(new DonutChart
        {
            Width = 120,
            Height = 120,
            CenterText = "12 M",
            CenterCaption = "Tokens",
            Slices = [new ChartSlice("Input", 1, ChartColor.Series1), new ChartSlice("Output", 3, ChartColor.Series2)],
        }));
        Rendered(Show(new DonutChart { Width = 120, Height = 120, Slices = [new ChartSlice("Input", 5, ChartColor.Series1)] }));
        Rendered(Show(new DonutChart { Width = 120, Height = 120, EmptyText = "leer" }));
        Rendered(Show(Calendar()));
        Rendered(Show(new CalendarHeatmap { Width = 360, EmptyText = "leer" }));
        Rendered(Show(new BarList { Width = 360, Items = [new BarListItem("Mcacc", 5, "5 k", ChartColor.Accent)] }));
        Rendered(Show(new ChartSwatch { Color = ChartColor.Series3 }));
    }

    /// <summary>Wednesday 2026-09-02 to Thursday 2026-09-24: four weeks, the first and the last one partly.</summary>
    private static CalendarHeatmap Calendar() => new()
    {
        Width = 600,
        WeekdayLabels = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"],
        MonthFormatter = day => day.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture),
        LessText = "Weniger",
        MoreText = "Mehr",
        Days = Enumerable.Range(0, 23)
            .Select(i => new CalendarDay(new DateOnly(2026, 9, 2).AddDays(i), i % 4, $"t{i}"))
            .ToList(),
    };

    [AvaloniaFact]
    public void A_calendar_tells_the_day_under_the_pointer_and_leaves_out_days_outside_its_range()
    {
        var chart = Calendar();
        Show(chart);
        chart.Bounds.Height.ShouldBeGreaterThan(7 * 14);

        // Four weeks fill the width at the largest cells; the first column is the week of Monday 2026-08-31.
        const double pitch = 22;
        Point Cell(int week, int row) => new(
            CalendarHeatmap.DayLabels + week * pitch + 5,
            CalendarHeatmap.MonthLabels + row * pitch + 5);

        chart.TooltipAt(Cell(0, 2)).ShouldBe("t0");
        chart.TooltipAt(Cell(3, 3)).ShouldBe("t22");
        chart.TooltipAt(Cell(0, 0)).ShouldBeNull();
        chart.TooltipAt(Cell(3, 4)).ShouldBeNull();
        chart.TooltipAt(new Point(2, 30)).ShouldBeNull();
    }

    [Fact]
    public void The_calendar_shades_by_quartile_and_the_highest_day_darkest()
    {
        var scale = CalendarHeatmap.ScaleOf([0, 1, 2, 3, 4, 5, 6, 7, 8]);

        CalendarHeatmap.LevelOf(0, scale).ShouldBe(0);
        CalendarHeatmap.LevelOf(1, scale).ShouldBe(1);
        CalendarHeatmap.LevelOf(4, scale).ShouldBe(2);
        CalendarHeatmap.LevelOf(6, scale).ShouldBe(3);
        CalendarHeatmap.LevelOf(7, scale).ShouldBe(4);
        CalendarHeatmap.LevelOf(8, scale).ShouldBe(4);
        CalendarHeatmap.LevelOf(5, CalendarHeatmap.ScaleOf([5])).ShouldBe(4);
    }

    [Fact]
    public void A_calendar_week_starts_on_monday()
    {
        CalendarHeatmap.MondayOf(new DateOnly(2026, 9, 24)).ShouldBe(new DateOnly(2026, 9, 21));
        CalendarHeatmap.MondayOf(new DateOnly(2026, 9, 21)).ShouldBe(new DateOnly(2026, 9, 21));
        CalendarHeatmap.MondayOf(new DateOnly(2026, 9, 27)).ShouldBe(new DateOnly(2026, 9, 21));
    }

    [AvaloniaFact]
    public void A_line_chart_tells_the_point_nearest_to_the_pointer()
    {
        var chart = TwoPointLine();
        Show(chart);

        chart.TooltipAt(new Point(LineChart.AxisWidth + 2, 50)).ShouldBe("a");
        chart.TooltipAt(new Point(chart.Bounds.Width - LineChart.RightPadding - 2, 50)).ShouldBe("b");
        chart.TooltipAt(new Point(chart.Bounds.Width / 2, 50)).ShouldBeNull();
    }

    [AvaloniaFact]
    public void Moving_the_pointer_over_a_chart_sets_its_tooltip()
    {
        var chart = TwoPointLine();
        var window = Show(chart);

        var point = chart.TranslatePoint(new Point(chart.Bounds.Width - LineChart.RightPadding - 2, 50), window)!.Value;
        window.MouseMove(point);

        ToolTip.GetTip(chart).ShouldBe("b");
    }

    [AvaloniaFact]
    public void A_bar_chart_tells_the_bar_under_the_pointer()
    {
        var chart = new StackedBarChart
        {
            Width = 360,
            Height = 160,
            Bars =
            [
                new BarStack("1.9.", [new BarSegment(1, ChartColor.Series1)], "erster"),
                new BarStack("2.9.", [new BarSegment(2, ChartColor.Series1)], "zweiter"),
            ],
        };
        Show(chart);

        chart.TooltipAt(new Point(StackedBarChart.AxisWidth + 1, 40)).ShouldBe("erster");
        chart.TooltipAt(new Point(chart.Bounds.Width - 1, 40)).ShouldBe("zweiter");
        chart.TooltipAt(new Point(1, 40)).ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_donut_tells_the_slice_under_the_pointer()
    {
        var chart = new DonutChart
        {
            Width = 120,
            Height = 120,
            Slices = [new ChartSlice("a", 1, ChartColor.Series1, "rechts"), new ChartSlice("b", 1, ChartColor.Series2, "links")],
        };
        Show(chart);

        // The first slice starts at the top and runs clockwise over the right half.
        chart.TooltipAt(new Point(61, 9)).ShouldBe("rechts");
        chart.TooltipAt(new Point(59, 9)).ShouldBe("links");
        chart.TooltipAt(new Point(60, 60)).ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_bar_list_is_one_row_per_item_high_and_tells_the_row_under_the_pointer()
    {
        var list = new BarList
        {
            Width = 360,
            Items =
            [
                new BarListItem("A", 3, "3", ChartColor.Accent, "eins"),
                new BarListItem("B", 2, "2", ChartColor.Accent, "zwei"),
                new BarListItem("C", 1, "1", ChartColor.Accent, "drei"),
            ],
        };
        var window = Show(new StackPanel { Children = { list } });
        window.UpdateLayout();

        list.Bounds.Height.ShouldBe(3 * BarList.RowHeight);
        list.TooltipAt(new Point(10, BarList.RowHeight + 5)).ShouldBe("zwei");
    }
}
