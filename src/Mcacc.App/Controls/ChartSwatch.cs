using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mcacc.App.Controls;

/// <summary>The small color square of a legend entry.</summary>
public sealed class ChartSwatch : Control
{
    private const double Side = 10;

    public static readonly StyledProperty<ChartColor> ColorProperty =
        AvaloniaProperty.Register<ChartSwatch, ChartColor>(nameof(Color));

    static ChartSwatch()
    {
        AffectsRender<ChartSwatch>(ColorProperty);
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

    public override void Render(DrawingContext context) =>
        context.DrawRectangle(ChartControl.BrushOf(this, Color), null, new Rect(0, 0, Side, Side), 2, 2);

    protected override Size MeasureOverride(Size availableSize) => new(Side, Side);
}
