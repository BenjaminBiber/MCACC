using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mcacc.App.Controls;

/// <summary>The small color square of a legend entry, or a short dashed line for an estimate.</summary>
public sealed class ChartSwatch : Control
{
    private const double Side = 10;
    private const double DashWidth = 16;

    public static readonly StyledProperty<ChartColor> ColorProperty =
        AvaloniaProperty.Register<ChartSwatch, ChartColor>(nameof(Color));

    public static readonly StyledProperty<bool> DashedProperty =
        AvaloniaProperty.Register<ChartSwatch, bool>(nameof(Dashed));

    static ChartSwatch()
    {
        AffectsRender<ChartSwatch>(ColorProperty, DashedProperty);
        AffectsMeasure<ChartSwatch>(DashedProperty);
    }

    public ChartSwatch()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public ChartColor Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Drawn like the dashed estimate of a line chart instead of a square.</summary>
    public bool Dashed
    {
        get => GetValue(DashedProperty);
        set => SetValue(DashedProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var brush = ChartControl.BrushOf(this, Color);
        if (!Dashed)
        {
            context.DrawRectangle(brush, null, new Rect(0, 0, Side, Side), 2, 2);
            return;
        }

        var pen = new Pen(brush, 1.5, new DashStyle([4, 3], 0), PenLineCap.Round);
        context.DrawLine(pen, new Point(1, Side / 2), new Point(DashWidth - 1, Side / 2));
    }

    protected override Size MeasureOverride(Size availableSize) => new(Dashed ? DashWidth : Side, Side);
}
