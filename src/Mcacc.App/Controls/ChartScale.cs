namespace Mcacc.App.Controls;

/// <summary>The arithmetic of the charts, kept apart from drawing so it can be tested without rendering.</summary>
public static class ChartScale
{
    private static readonly double[] NiceSteps = [1, 2, 2.5, 5, 10];

    /// <summary>The smallest of 1, 2, 2.5, 5 times a power of ten that is at least <paramref name="value"/>.</summary>
    public static double NiceMax(double value)
    {
        if (!(value > 0) || double.IsInfinity(value))
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var step in NiceSteps)
        {
            // The tolerance keeps 100 at 100 although Log10 and Pow round.
            if (value <= step * magnitude * (1 + 1e-9))
            {
                return step * magnitude;
            }
        }

        return 10 * magnitude;
    }

    /// <returns><paramref name="count"/> + 1 values from 0 to <paramref name="max"/>.</returns>
    public static IReadOnlyList<double> Ticks(double max, int count = 4) =>
        Enumerable.Range(0, count + 1).Select(i => max * i / count).ToList();

    /// <summary>Maps a value between min and max onto the pixels from..to; to may be smaller, as on a y axis.</summary>
    public static double Map(double value, double min, double max, double from, double to) =>
        max <= min ? from : from + (Math.Clamp(value, min, max) - min) / (max - min) * (to - from);

    /// <returns>The index of the band under x when the width is split into count equal bands, or -1.</returns>
    public static int BandAt(double x, double width, int count) =>
        count <= 0 || width <= 0 || x < 0 || x >= width ? -1 : Math.Min(count - 1, (int)(x / (width / count)));
}
