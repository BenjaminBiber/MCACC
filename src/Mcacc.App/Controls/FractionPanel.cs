using Avalonia;
using Avalonia.Controls;

namespace Mcacc.App.Controls;

/// <summary>
/// Lays its children out from the left edge across <see cref="Fraction"/> of its width, the fill of a progress track.
/// A control rather than grid columns, because a template binding does not reach a column definition.
/// </summary>
public sealed class FractionPanel : Panel
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<FractionPanel, double>(nameof(Fraction));

    static FractionPanel() => AffectsArrange<FractionPanel>(FractionProperty);

    /// <summary>0 to 1; anything outside is clamped, and a value that is not finite reads as 0.</summary>
    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var fraction = double.IsFinite(Fraction) ? Math.Clamp(Fraction, 0, 1) : 0;
        var fill = new Rect(0, 0, finalSize.Width * fraction, finalSize.Height);
        foreach (var child in Children)
        {
            child.Arrange(fill);
        }

        return finalSize;
    }
}
