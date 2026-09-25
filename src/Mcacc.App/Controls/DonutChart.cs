using Avalonia;
using Avalonia.Media;
using Mcacc.App.Theme;

namespace Mcacc.App.Controls;

/// <summary>A ring of slices, starting at the top and running clockwise, with a text in the middle.</summary>
public sealed class DonutChart : ChartControl
{
    private const double EdgeMargin = 2;

    public static readonly StyledProperty<IReadOnlyList<ChartSlice>?> SlicesProperty =
        AvaloniaProperty.Register<DonutChart, IReadOnlyList<ChartSlice>?>(nameof(Slices));

    public static readonly StyledProperty<string?> CenterTextProperty =
        AvaloniaProperty.Register<DonutChart, string?>(nameof(CenterText));

    public static readonly StyledProperty<string?> CenterCaptionProperty =
        AvaloniaProperty.Register<DonutChart, string?>(nameof(CenterCaption));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(Thickness), 14);

    public static readonly StyledProperty<double> CenterTextSizeProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(CenterTextSize), 18);

    public static readonly StyledProperty<double> CenterCaptionSizeProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(CenterCaptionSize), 11);

    static DonutChart()
    {
        AffectsRender<DonutChart>(
            SlicesProperty,
            CenterTextProperty,
            CenterCaptionProperty,
            ThicknessProperty,
            CenterTextSizeProperty,
            CenterCaptionSizeProperty);
    }

    public IReadOnlyList<ChartSlice>? Slices
    {
        get => GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public string? CenterText
    {
        get => GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public string? CenterCaption
    {
        get => GetValue(CenterCaptionProperty);
        set => SetValue(CenterCaptionProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public double CenterTextSize
    {
        get => GetValue(CenterTextSizeProperty);
        set => SetValue(CenterTextSizeProperty, value);
    }

    public double CenterCaptionSize
    {
        get => GetValue(CenterCaptionSizeProperty);
        set => SetValue(CenterCaptionSizeProperty, value);
    }

    private double Total => Slices?.Where(slice => slice.Value > 0).Sum(slice => slice.Value) ?? 0;

    protected override bool HasData => Total > 0;

    public override string? TooltipAt(Point point)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = Radius(Bounds.Size);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < radius - Thickness - EdgeMargin || distance > radius + EdgeMargin)
        {
            return null;
        }

        // Degrees clockwise from the top, like the slices.
        var angle = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
        var start = 0d;
        foreach (var slice in (Slices ?? []).Where(slice => slice.Value > 0))
        {
            var sweep = slice.Value / Total * 360;
            if (angle < start + sweep)
            {
                return slice.Tooltip;
            }

            start += sweep;
        }

        return null;
    }

    protected override void RenderChart(DrawingContext context, Size size)
    {
        var center = new Point(size.Width / 2, size.Height / 2);
        var ring = Radius(size) - Thickness / 2;
        var slices = Slices!.Where(slice => slice.Value > 0).ToList();
        if (slices.Count == 1)
        {
            context.DrawEllipse(null, new Pen(Brush(slices[0].Color), Thickness), center, ring, ring);
        }
        else
        {
            var start = 0d;
            foreach (var slice in slices)
            {
                var sweep = slice.Value / Total * 360;
                DrawArc(context, center, ring, start, sweep, new Pen(Brush(slice.Color), Thickness, lineCap: PenLineCap.Flat));
                start += sweep;
            }
        }

        var y = center.Y;
        if (CenterText is { } text)
        {
            var label = Label(text, CenterTextSize, ThemeKeys.Text, ring * 1.6, FontWeight.SemiBold);
            y -= CenterCaption is null ? label.Height / 2 : label.Height * 0.75;
            context.DrawText(label, new Point(center.X - label.Width / 2, y));
            y += label.Height;
        }

        if (CenterCaption is { } caption)
        {
            var label = Label(caption, CenterCaptionSize, maxWidth: ring * 1.6);
            context.DrawText(label, new Point(center.X - label.Width / 2, CenterText is null ? y - label.Height / 2 : y));
        }
    }

    private static void DrawArc(DrawingContext context, Point center, double radius, double start, double sweep, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(PointAt(center, radius, start), isFilled: false);
            figure.ArcTo(
                PointAt(center, radius, start + sweep),
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: sweep > 180,
                SweepDirection.Clockwise);
            figure.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    /// <param name="degrees">Clockwise from the top.</param>
    private static Point PointAt(Point center, double radius, double degrees)
    {
        var radians = (degrees - 90) * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private static double Radius(Size size) => Math.Max(1, Math.Min(size.Width, size.Height) / 2 - EdgeMargin);
}
