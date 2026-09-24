using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Mcacc.App.Controls;

/// <summary>One stacked bar per entry, e.g. per day; labels are thinned out so they do not overlap.</summary>
public sealed class StackedBarChart : ChartControl
{
    internal const double AxisWidth = 48;
    private const double RightPadding = 4;
    private const double TopPadding = 6;
    private const double BottomLabels = 18;
    private const double MaxBarWidth = 28;
    private const double LabelSpace = 36;

    public static readonly StyledProperty<IReadOnlyList<BarStack>?> BarsProperty =
        AvaloniaProperty.Register<StackedBarChart, IReadOnlyList<BarStack>?>(nameof(Bars));

    public static readonly StyledProperty<Func<double, string>?> AxisFormatterProperty =
        AvaloniaProperty.Register<StackedBarChart, Func<double, string>?>(nameof(AxisFormatter));

    static StackedBarChart()
    {
        AffectsRender<StackedBarChart>(BarsProperty, AxisFormatterProperty);
    }

    public IReadOnlyList<BarStack>? Bars
    {
        get => GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    /// <summary>Writes the values of the axis; without it they are plain numbers.</summary>
    public Func<double, string>? AxisFormatter
    {
        get => GetValue(AxisFormatterProperty);
        set => SetValue(AxisFormatterProperty, value);
    }

    protected override bool HasData => Bars?.Any(bar => bar.Total > 0) == true;

    public override string? TooltipAt(Point point)
    {
        var bars = Bars ?? [];
        var plot = Plot(Bounds.Size);
        // The right padding is dead space, not another band; a pointer there still belongs to the last bar.
        var x = Math.Min(point.X - plot.Left, plot.Width - 1);
        var index = ChartScale.BandAt(x, plot.Width, bars.Count);
        return index < 0 ? null : bars[index].Tooltip;
    }

    protected override void RenderChart(DrawingContext context, Size size)
    {
        var bars = Bars!;
        var plot = Plot(size);
        var max = ChartScale.NiceMax(bars.Max(bar => bar.Total));
        var track = new Pen(Brush(ChartColor.Track), 1);
        foreach (var tick in ChartScale.Ticks(max))
        {
            var y = Math.Round(ChartScale.Map(tick, 0, max, plot.Bottom, plot.Top)) + 0.5;
            context.DrawLine(track, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = AxisFormatter?.Invoke(tick) ?? tick.ToString("0", CultureInfo.CurrentCulture);
            var label = Label(text, 10);
            context.DrawText(label, new Point(plot.Left - 6 - label.Width, y - label.Height / 2));
        }

        var band = plot.Width / bars.Count;
        var width = Math.Min(MaxBarWidth, band * 0.7);
        var every = Math.Max(1, (int)Math.Ceiling(LabelSpace / band));
        for (var i = 0; i < bars.Count; i++)
        {
            var center = plot.Left + band * (i + 0.5);
            var sum = 0d;
            var bottom = plot.Bottom;
            foreach (var segment in bars[i].Segments.Where(segment => segment.Value > 0))
            {
                sum += segment.Value;
                var top = ChartScale.Map(sum, 0, max, plot.Bottom, plot.Top);
                context.FillRectangle(Brush(segment.Color), new Rect(center - width / 2, top, width, bottom - top));
                bottom = top;
            }

            if (i % every == 0)
            {
                var label = Label(bars[i].Label, 10, maxWidth: band * every);
                context.DrawText(label, new Point(center - label.Width / 2, plot.Bottom + 4));
            }
        }
    }

    private static Rect Plot(Size size) => new(
        AxisWidth,
        TopPadding,
        Math.Max(1, size.Width - AxisWidth - RightPadding),
        Math.Max(1, size.Height - TopPadding - BottomLabels));
}
