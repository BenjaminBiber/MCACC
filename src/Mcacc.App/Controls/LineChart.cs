using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Mcacc.App.Theme;

namespace Mcacc.App.Controls;

/// <summary>
/// Lines over a value axis from <see cref="Minimum"/> to <see cref="Maximum"/>, with an optional dashed threshold.
/// A line breaks where two points lie further apart than <see cref="MaxGap"/>, e.g. while the app was closed.
/// </summary>
public sealed class LineChart : ChartControl
{
    internal const double AxisWidth = 36;
    internal const double RightPadding = 6;
    private const double TopPadding = 6;
    private const double BottomLabels = 18;
    private const double HitDistance = 12;
    private const double LabelGap = 8;

    public static readonly StyledProperty<IReadOnlyList<LineSeries>?> SeriesProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<LineSeries>?>(nameof(Series));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(Threshold), double.NaN);

    public static readonly StyledProperty<double> XMinProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(XMin));

    public static readonly StyledProperty<double> XMaxProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(XMax), 1);

    public static readonly StyledProperty<double> MaxGapProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(MaxGap), double.PositiveInfinity);

    public static readonly StyledProperty<string> YSuffixProperty =
        AvaloniaProperty.Register<LineChart, string>(nameof(YSuffix), " %");

    public static readonly StyledProperty<string?> StartLabelProperty =
        AvaloniaProperty.Register<LineChart, string?>(nameof(StartLabel));

    public static readonly StyledProperty<string?> EndLabelProperty =
        AvaloniaProperty.Register<LineChart, string?>(nameof(EndLabel));

    public static readonly StyledProperty<IReadOnlyList<AxisTick>?> XTicksProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<AxisTick>?>(nameof(XTicks));

    static LineChart()
    {
        AffectsRender<LineChart>(
            SeriesProperty,
            MinimumProperty,
            MaximumProperty,
            ThresholdProperty,
            XMinProperty,
            XMaxProperty,
            MaxGapProperty,
            YSuffixProperty,
            StartLabelProperty,
            EndLabelProperty,
            XTicksProperty);
    }

    public IReadOnlyList<LineSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Drawn as a dashed line in the danger color; NaN draws none.</summary>
    public double Threshold
    {
        get => GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    public double XMin
    {
        get => GetValue(XMinProperty);
        set => SetValue(XMinProperty, value);
    }

    public double XMax
    {
        get => GetValue(XMaxProperty);
        set => SetValue(XMaxProperty, value);
    }

    /// <summary>In x units: two points further apart are not connected.</summary>
    public double MaxGap
    {
        get => GetValue(MaxGapProperty);
        set => SetValue(MaxGapProperty, value);
    }

    public string YSuffix
    {
        get => GetValue(YSuffixProperty);
        set => SetValue(YSuffixProperty, value);
    }

    /// <summary>Written under the left end of the x axis.</summary>
    public string? StartLabel
    {
        get => GetValue(StartLabelProperty);
        set => SetValue(StartLabelProperty, value);
    }

    /// <summary>Written under the right end of the x axis.</summary>
    public string? EndLabel
    {
        get => GetValue(EndLabelProperty);
        set => SetValue(EndLabelProperty, value);
    }

    /// <summary>
    /// Marks between the ends of the x axis; a label that would run into the start or end label or into the one
    /// before it is left out, its grid line stays.
    /// </summary>
    public IReadOnlyList<AxisTick>? XTicks
    {
        get => GetValue(XTicksProperty);
        set => SetValue(XTicksProperty, value);
    }

    protected override bool HasData => Series?.Any(series => series.Points.Count > 0) == true;

    public override string? TooltipAt(Point point)
    {
        var plot = Plot(Bounds.Size);
        var nearest = (Series ?? [])
            .SelectMany(series => series.Points)
            .Select(p => (Point: p, Distance: Math.Abs(X(p.X, plot) - point.X)))
            .Where(entry => entry.Distance <= HitDistance)
            .OrderBy(entry => entry.Distance)
            .Select(entry => entry.Point)
            .FirstOrDefault();
        return nearest?.Tooltip;
    }

    protected override void RenderChart(DrawingContext context, Size size)
    {
        var plot = Plot(size);
        var track = new Pen(Brush(ChartColor.Track), 1);
        foreach (var tick in ChartScale.Ticks(Maximum - Minimum))
        {
            var value = Minimum + tick;
            var y = Math.Round(Y(value, plot)) + 0.5;
            context.DrawLine(track, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = Label(string.Create(CultureInfo.CurrentCulture, $"{value:0}{YSuffix}"), 10);
            context.DrawText(label, new Point(plot.Left - 6 - label.Width, y - label.Height / 2));
        }

        var ticks = (XTicks ?? []).Where(tick => tick.X >= XMin && tick.X <= XMax).OrderBy(tick => tick.X).ToList();
        foreach (var tick in ticks)
        {
            var x = Math.Round(X(tick.X, plot)) + 0.5;
            context.DrawLine(track, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }

        if (!double.IsNaN(Threshold))
        {
            var y = Y(Threshold, plot);
            var dashed = new Pen(Brush(ChartColor.Danger), 1, new DashStyle([4, 3], 0));
            context.DrawLine(dashed, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        foreach (var series in Series ?? [])
        {
            DrawSeries(context, series, plot);
        }

        DrawXLabels(context, ticks, plot);
    }

    private void DrawXLabels(DrawingContext context, List<AxisTick> ticks, Rect plot)
    {
        var top = plot.Bottom + 4;
        var left = double.NegativeInfinity;
        var right = double.PositiveInfinity;
        if (StartLabel is { } start)
        {
            var label = Label(start, 10);
            context.DrawText(label, new Point(plot.Left, top));
            left = plot.Left + label.Width;
        }

        if (EndLabel is { } end)
        {
            var label = Label(end, 10);
            context.DrawText(label, new Point(plot.Right - label.Width, top));
            right = plot.Right - label.Width;
        }

        foreach (var tick in ticks)
        {
            var label = Label(tick.Label, 10);
            var x = Math.Clamp(X(tick.X, plot) - label.Width / 2, 0, Math.Max(0, Bounds.Width - label.Width));
            if (x < left + LabelGap || x + label.Width > right - LabelGap)
            {
                continue;
            }

            context.DrawText(label, new Point(x, top));
            left = x + label.Width;
        }
    }

    private void DrawSeries(DrawingContext context, LineSeries series, Rect plot)
    {
        var brush = Brush(series.Color);
        var pen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var points = series.Points.OrderBy(p => p.X).ToList();
        var run = new List<Point>();
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0 && points[i].X - points[i - 1].X > MaxGap)
            {
                DrawRun(context, run, pen, brush);
                run.Clear();
            }

            run.Add(new Point(X(points[i].X, plot), Y(points[i].Y, plot)));
        }

        DrawRun(context, run, pen, brush);
    }

    /// <summary>A lone point has no line to show it, so it gets a dot.</summary>
    private static void DrawRun(DrawingContext context, List<Point> run, Pen pen, IBrush brush)
    {
        if (run.Count == 1)
        {
            context.DrawEllipse(brush, null, run[0], 2.5, 2.5);
            return;
        }

        if (run.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(run[0], isFilled: false);
            foreach (var point in run.Skip(1))
            {
                figure.LineTo(point);
            }

            figure.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static Rect Plot(Size size) => new(
        AxisWidth,
        TopPadding,
        Math.Max(1, size.Width - AxisWidth - RightPadding),
        Math.Max(1, size.Height - TopPadding - BottomLabels));

    private double X(double x, Rect plot) => ChartScale.Map(x, XMin, XMax, plot.Left, plot.Right);

    private double Y(double y, Rect plot) => ChartScale.Map(y, Minimum, Maximum, plot.Bottom, plot.Top);
}
