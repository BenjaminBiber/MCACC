namespace Mcacc.App.Controls;

/// <summary>A color of the chart palette; the control looks up the brush in the current theme.</summary>
public enum ChartColor
{
    Series1,
    Series2,
    Series3,
    Series4,
    Series5,
    Series6,
    Accent,
    Danger,
    Muted,
    Track,
}

/// <param name="Tooltip">What the chart shows when the pointer rests on this point.</param>
public sealed record ChartPoint(double X, double Y, string? Tooltip = null);

/// <summary>A labeled mark on the x axis of a line chart, with a faint grid line above it.</summary>
public sealed record AxisTick(double X, string Label);

public sealed record LineSeries(string Name, ChartColor Color, IReadOnlyList<ChartPoint> Points)
{
    /// <summary>Guessed stretches where nothing was measured, drawn dashed; they carry no tooltips.</summary>
    public IReadOnlyList<IReadOnlyList<ChartPoint>> Estimated { get; init; } = [];
}

public sealed record BarSegment(double Value, ChartColor Color);

/// <summary>One bar of a stacked bar chart; the segments are drawn from the bottom up.</summary>
public sealed record BarStack(string Label, IReadOnlyList<BarSegment> Segments, string? Tooltip = null)
{
    public double Total => Segments.Sum(segment => segment.Value);
}

/// <summary>One day of a calendar heatmap.</summary>
/// <param name="Estimated">The value is an estimate; the cell is struck through.</param>
public sealed record CalendarDay(DateOnly Day, double Value, string? Tooltip = null, bool Estimated = false);

public sealed record ChartSlice(string Label, double Value, ChartColor Color, string? Tooltip = null);

public sealed record BarListItem(string Label, double Value, string ValueText, ChartColor Color, string? Tooltip = null);
