using Avalonia;
using Avalonia.Media;

namespace Mcacc.App.Controls;

/// <summary>
/// One cell per day, a column per week from Monday to Sunday, the latest week on the right. The shade of a day is its
/// quartile among the shown days with a value, the highest day always the darkest. As many weeks as fit are shown;
/// the cells grow to fill the width when all weeks fit.
/// </summary>
public sealed class CalendarHeatmap : ChartControl
{
    internal const double DayLabels = 26;
    internal const double MonthLabels = 16;
    private const double MinPitch = 14;
    private const double MaxPitch = 22;
    private const double Gap = 3;
    private const double LegendHeight = 24;
    private const double LegendCell = 10;

    private static readonly double[] Shades = [0.3, 0.5, 0.75, 1];

    public static readonly StyledProperty<IReadOnlyList<CalendarDay>?> DaysProperty =
        AvaloniaProperty.Register<CalendarHeatmap, IReadOnlyList<CalendarDay>?>(nameof(Days));

    public static readonly StyledProperty<ChartColor> ColorProperty =
        AvaloniaProperty.Register<CalendarHeatmap, ChartColor>(nameof(Color), ChartColor.Accent);

    public static readonly StyledProperty<IReadOnlyList<string>?> WeekdayLabelsProperty =
        AvaloniaProperty.Register<CalendarHeatmap, IReadOnlyList<string>?>(nameof(WeekdayLabels));

    public static readonly StyledProperty<Func<DateOnly, string>?> MonthFormatterProperty =
        AvaloniaProperty.Register<CalendarHeatmap, Func<DateOnly, string>?>(nameof(MonthFormatter));

    public static readonly StyledProperty<string?> LessTextProperty =
        AvaloniaProperty.Register<CalendarHeatmap, string?>(nameof(LessText));

    public static readonly StyledProperty<string?> MoreTextProperty =
        AvaloniaProperty.Register<CalendarHeatmap, string?>(nameof(MoreText));

    static CalendarHeatmap()
    {
        AffectsMeasure<CalendarHeatmap>(DaysProperty);
        AffectsRender<CalendarHeatmap>(
            DaysProperty,
            ColorProperty,
            WeekdayLabelsProperty,
            MonthFormatterProperty,
            LessTextProperty,
            MoreTextProperty);
    }

    /// <summary>Consecutive days, oldest first.</summary>
    public IReadOnlyList<CalendarDay>? Days
    {
        get => GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    public ChartColor Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Seven names from Monday on; every second one is written left of its row.</summary>
    public IReadOnlyList<string>? WeekdayLabels
    {
        get => GetValue(WeekdayLabelsProperty);
        set => SetValue(WeekdayLabelsProperty, value);
    }

    /// <summary>Names the month above the week its first day falls in; without it no months are written.</summary>
    public Func<DateOnly, string>? MonthFormatter
    {
        get => GetValue(MonthFormatterProperty);
        set => SetValue(MonthFormatterProperty, value);
    }

    /// <summary>Written left of the shade legend.</summary>
    public string? LessText
    {
        get => GetValue(LessTextProperty);
        set => SetValue(LessTextProperty, value);
    }

    /// <summary>Written right of the shade legend.</summary>
    public string? MoreText
    {
        get => GetValue(MoreTextProperty);
        set => SetValue(MoreTextProperty, value);
    }

    protected override bool HasData => Days is { Count: > 0 };

    public override string? TooltipAt(Point point)
    {
        var layout = LayoutFor(Bounds.Width);
        var week = (int)Math.Floor((point.X - DayLabels) / layout.Pitch);
        var row = (int)Math.Floor((point.Y - MonthLabels) / layout.Pitch);
        if (week < 0 || week >= layout.Weeks || row < 0 || row > 6)
        {
            return null;
        }

        var day = layout.FirstMonday.AddDays(week * 7 + row);
        return (Days ?? []).FirstOrDefault(entry => entry.Day == day)?.Tooltip;
    }

    /// <summary>The values the shades are measured against: the positive ones, ascending.</summary>
    internal static IReadOnlyList<double> ScaleOf(IEnumerable<double> values) => values.Where(v => v > 0).Order().ToList();

    /// <param name="sorted">From <see cref="ScaleOf"/>.</param>
    /// <returns>The shade of a value: 0 for none, 1 to 4 by the quartiles, 4 for the highest value.</returns>
    internal static int LevelOf(double value, IReadOnlyList<double> sorted)
    {
        if (value <= 0)
        {
            return 0;
        }

        if (sorted.Count == 0 || value >= sorted[^1])
        {
            return 4;
        }

        double Quartile(double p) => sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];
        return value <= Quartile(0.25) ? 1 : value <= Quartile(0.5) ? 2 : value <= Quartile(0.75) ? 3 : 4;
    }

    internal static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(0, MonthLabels + 7 * LayoutFor(width).Pitch + LegendHeight);
    }

    protected override void RenderChart(DrawingContext context, Size size)
    {
        var layout = LayoutFor(size.Width);
        var byDay = Days!.ToDictionary(entry => entry.Day);
        var last = layout.FirstMonday.AddDays(layout.Weeks * 7 - 1);
        var shown = ScaleOf(byDay.Values
            .Where(entry => entry.Day >= layout.FirstMonday && entry.Day <= last)
            .Select(entry => entry.Value));
        var shades = Shading();
        var cell = layout.Pitch - Gap;
        var strike = new Pen(Brush(ChartColor.Track), 1.5);

        for (var week = 0; week < layout.Weeks; week++)
        {
            for (var row = 0; row < 7; row++)
            {
                if (!byDay.TryGetValue(layout.FirstMonday.AddDays(week * 7 + row), out var day))
                {
                    continue;
                }

                var rect = new Rect(DayLabels + week * layout.Pitch, MonthLabels + row * layout.Pitch, cell, cell);
                context.DrawRectangle(shades[LevelOf(day.Value, shown)], null, rect, 2, 2);
                if (day.Estimated)
                {
                    context.DrawLine(strike, rect.BottomLeft + new Vector(2, -2), rect.TopRight + new Vector(-2, 2));
                }
            }
        }

        DrawWeekdays(context, layout.Pitch, cell);
        DrawMonths(context, layout);
        DrawLegend(context, size, layout.Pitch, shades);
    }

    private void DrawWeekdays(DrawingContext context, double pitch, double cell)
    {
        var names = WeekdayLabels ?? [];
        for (var row = 0; row < Math.Min(7, names.Count); row += 2)
        {
            var label = Label(names[row], 10, maxWidth: DayLabels - 4);
            context.DrawText(label, new Point(0, MonthLabels + row * pitch + (cell - label.Height) / 2));
        }
    }

    private void DrawMonths(DrawingContext context, Layout layout)
    {
        if (MonthFormatter is not { } format)
        {
            return;
        }

        var right = double.NegativeInfinity;
        for (var week = 0; week < layout.Weeks; week++)
        {
            var monday = layout.FirstMonday.AddDays(week * 7);
            var first = Enumerable.Range(0, 7).Select(monday.AddDays).FirstOrDefault(day => day.Day == 1);
            if (first == default)
            {
                continue;
            }

            var x = DayLabels + week * layout.Pitch;
            var label = Label(format(first), 10);
            if (x < right + 6 || x + label.Width > Bounds.Width)
            {
                continue;
            }

            context.DrawText(label, new Point(x, 0));
            right = x + label.Width;
        }
    }

    private void DrawLegend(DrawingContext context, Size size, double pitch, IBrush[] shades)
    {
        var y = MonthLabels + 7 * pitch + 8;
        var x = size.Width;
        if (MoreText is { } more)
        {
            var label = Label(more, 10);
            x -= label.Width;
            context.DrawText(label, new Point(x, y + (LegendCell - label.Height) / 2));
            x -= 6;
        }

        for (var level = shades.Length - 1; level >= 0; level--)
        {
            x -= LegendCell;
            context.DrawRectangle(shades[level], null, new Rect(x, y, LegendCell, LegendCell), 2, 2);
            x -= 3;
        }

        if (LessText is { } less)
        {
            var label = Label(less, 10);
            context.DrawText(label, new Point(x - 3 - label.Width, y + (LegendCell - label.Height) / 2));
        }
    }

    /// <summary>The empty shade and the four shades of <see cref="Color"/>.</summary>
    private IBrush[] Shading()
    {
        var color = Brush(Color) is ISolidColorBrush solid ? solid.Color : Colors.Gray;
        return [Brush(ChartColor.Track), .. Shades.Select(opacity => new SolidColorBrush(color, opacity))];
    }

    private Layout LayoutFor(double width)
    {
        var days = Days ?? [];
        if (days.Count == 0)
        {
            return new Layout(MinPitch, 0, default);
        }

        var lastMonday = MondayOf(days[^1].Day);
        var total = (lastMonday.DayNumber - MondayOf(days[0].Day).DayNumber) / 7 + 1;
        var available = Math.Max(0, width - DayLabels);
        var pitch = Math.Clamp(available / total, MinPitch, MaxPitch);
        var weeks = Math.Clamp((int)Math.Floor(available / pitch + 1e-9), 1, total);
        return new Layout(pitch, weeks, lastMonday.AddDays(-7 * (weeks - 1)));
    }

    private readonly record struct Layout(double Pitch, int Weeks, DateOnly FirstMonday);
}
