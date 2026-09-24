using Avalonia;
using Avalonia.Media;
using Mcacc.App.Theme;

namespace Mcacc.App.Controls;

/// <summary>Horizontal bars, one row per item: label, bar relative to the largest value, value text.</summary>
public sealed class BarList : ChartControl
{
    internal const double RowHeight = 28;
    private const double BarHeight = 6;

    public static readonly StyledProperty<IReadOnlyList<BarListItem>?> ItemsProperty =
        AvaloniaProperty.Register<BarList, IReadOnlyList<BarListItem>?>(nameof(Items));

    static BarList()
    {
        AffectsRender<BarList>(ItemsProperty);
        AffectsMeasure<BarList>(ItemsProperty);
    }

    public IReadOnlyList<BarListItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override bool HasData => Items is { Count: > 0 };

    public override string? TooltipAt(Point point)
    {
        var items = Items ?? [];
        var row = (int)Math.Floor(point.Y / RowHeight);
        return row >= 0 && row < items.Count ? items[row].Tooltip : null;
    }

    /// <summary>Without items the list keeps one row, so the empty text has room.</summary>
    protected override Size MeasureOverride(Size availableSize) =>
        new(0, Math.Max(1, Items?.Count ?? 0) * RowHeight);

    protected override void RenderChart(DrawingContext context, Size size)
    {
        var items = Items!;
        var max = Math.Max(items.Max(item => item.Value), double.Epsilon);
        var labelWidth = size.Width * 0.38;
        var barLeft = labelWidth + 8;
        var barWidth = Math.Max(1, size.Width * 0.84 - barLeft);
        var track = Brush(ChartColor.Track);
        for (var i = 0; i < items.Count; i++)
        {
            var middle = i * RowHeight + RowHeight / 2;
            var label = Label(items[i].Label, 12, ThemeKeys.Text, labelWidth);
            context.DrawText(label, new Point(0, middle - label.Height / 2));

            var bar = new Rect(barLeft, middle - BarHeight / 2, barWidth, BarHeight);
            context.DrawRectangle(track, null, bar, BarHeight / 2, BarHeight / 2);
            var fill = bar.WithWidth(Math.Max(BarHeight, barWidth * items[i].Value / max));
            context.DrawRectangle(Brush(items[i].Color), null, fill, BarHeight / 2, BarHeight / 2);

            var value = Label(items[i].ValueText, 11, ThemeKeys.TextSecondary);
            context.DrawText(value, new Point(size.Width - value.Width, middle - value.Height / 2));
        }
    }
}
