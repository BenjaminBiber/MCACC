using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Mcacc.Core.Model;

namespace Mcacc.App.ViewModels;

public static class ColorConverters
{
    /// <summary>Turns an account color like <c>#D97757</c> into a brush; anything else stays transparent.</summary>
    public static readonly IValueConverter ToBrush = new FuncValueConverter<string?, IBrush>(color =>
        AccountPalette.IsValidColor(color)
            ? new SolidColorBrush(Color.Parse(color!))
            : Brushes.Transparent);

    /// <summary>Formats a percentage the way the usage line does; used by the views for tooltips.</summary>
    public static readonly IValueConverter ToPercent = new FuncValueConverter<double?, string>(value =>
        value is null ? "—" : string.Create(CultureInfo.CurrentCulture, $"{Math.Round(value.Value)} %"));
}
