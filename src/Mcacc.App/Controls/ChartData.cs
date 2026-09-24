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

public sealed record LineSeries(string Name, ChartColor Color, IReadOnlyList<ChartPoint> Points);

public sealed record BarSegment(double Value, ChartColor Color);

/// <summary>One bar of a stacked bar chart; the segments are drawn from the bottom up.</summary>
public sealed record BarStack(string Label, IReadOnlyList<BarSegment> Segments, string? Tooltip = null)
{
    public double Total => Segments.Sum(segment => segment.Value);
}

public sealed record ChartSlice(string Label, double Value, ChartColor Color, string? Tooltip = null);

public sealed record BarListItem(string Label, double Value, string ValueText, ChartColor Color, string? Tooltip = null);
