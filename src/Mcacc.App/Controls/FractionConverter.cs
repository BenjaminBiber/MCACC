using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Mcacc.App.Controls;

/// <summary>Turns a 0-1 fraction into the two star widths of a progress track.</summary>
public sealed class FractionConverter(bool filled) : IValueConverter
{
    public static readonly FractionConverter Star = new(filled: true);
    public static readonly FractionConverter Rest = new(filled: false);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value is double d && double.IsFinite(d) ? Math.Clamp(d, 0, 1) : 0;
        return new GridLength(filled ? fraction : 1 - fraction, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A track width is never written back.");
}
