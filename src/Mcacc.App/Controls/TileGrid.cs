using Avalonia;
using Avalonia.Controls;

namespace Mcacc.App.Controls;

/// <summary>
/// Lays tiles out in as many equal columns as fit at <see cref="MinColumnWidth"/>, stretched to the full width,
/// so a narrow window shows one full-width column instead of a fixed-width tile with a gap beside it.
/// Every tile gets the height of the tallest, so a short tile in a row of its own still matches the others.
/// </summary>
public sealed class TileGrid : Panel
{
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<TileGrid, double>(nameof(MinColumnWidth), 300);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<TileGrid, double>(nameof(Spacing), 12);

    private double _measuredColumnWidth = double.NaN;

    static TileGrid() => AffectsMeasure<TileGrid>(MinColumnWidthProperty, SpacingProperty);

    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <summary>The gap between columns and between rows.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private List<Control> Visible => Children.Where(child => child.IsVisible).ToList();

    protected override Size MeasureOverride(Size availableSize)
    {
        var tiles = Visible;
        if (tiles.Count == 0)
        {
            return default;
        }

        var (columns, columnWidth) = Columns(availableSize.Width, tiles.Count);
        _measuredColumnWidth = columnWidth;
        var tileHeight = TileHeight(tiles, columnWidth, measure: true);
        var rows = (tiles.Count + columns - 1) / columns;
        return new Size(
            columns * columnWidth + (columns - 1) * Spacing,
            rows * tileHeight + (rows - 1) * Spacing);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var tiles = Visible;
        var (columns, columnWidth) = Columns(finalSize.Width, tiles.Count);
        // A parent may arrange wider than it measured; the tiles then wrap their text at the new width.
        var tileHeight = TileHeight(tiles, columnWidth, measure: !columnWidth.Equals(_measuredColumnWidth));
        for (var i = 0; i < tiles.Count; i++)
        {
            var x = i % columns * (columnWidth + Spacing);
            var y = i / columns * (tileHeight + Spacing);
            tiles[i].Arrange(new Rect(x, y, columnWidth, tileHeight));
        }

        return finalSize;
    }

    /// <summary>Without a width limit, e.g. in a horizontal scroll, every tile gets its own column at the minimum width.</summary>
    private (int Columns, double Width) Columns(double available, int count)
    {
        if (!double.IsFinite(available))
        {
            return (Math.Max(1, count), MinColumnWidth);
        }

        var columns = Math.Max(1, (int)((available + Spacing) / (MinColumnWidth + Spacing)));
        var width = Math.Max(0, (available - (columns - 1) * Spacing) / columns);
        return (columns, width);
    }

    private static double TileHeight(List<Control> tiles, double columnWidth, bool measure)
    {
        var height = 0.0;
        foreach (var tile in tiles)
        {
            if (measure)
            {
                tile.Measure(new Size(columnWidth, double.PositiveInfinity));
            }

            height = Math.Max(height, tile.DesiredSize.Height);
        }

        return height;
    }
}
