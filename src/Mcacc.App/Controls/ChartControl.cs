using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Mcacc.App.Theme;

namespace Mcacc.App.Controls;

/// <summary>
/// The common part of the charts: theme colors, labels, the empty state and the tooltip that follows the pointer.
/// A chart draws itself in <see cref="RenderChart"/>; there is no template.
/// </summary>
public abstract class ChartControl : Control
{
    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<ChartControl, string?>(nameof(EmptyText));

    static ChartControl()
    {
        AffectsRender<ChartControl>(EmptyTextProperty);
    }

    protected ChartControl()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>Shown in the middle when there is nothing to draw.</summary>
    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    protected abstract bool HasData { get; }

    public static string KeyOf(ChartColor color) => color switch
    {
        ChartColor.Series1 => ThemeKeys.Series1,
        ChartColor.Series2 => ThemeKeys.Series2,
        ChartColor.Series3 => ThemeKeys.Series3,
        ChartColor.Series4 => ThemeKeys.Series4,
        ChartColor.Series5 => ThemeKeys.Series5,
        ChartColor.Series6 => ThemeKeys.Series6,
        ChartColor.Accent => ThemeKeys.Accent,
        ChartColor.Danger => ThemeKeys.Danger,
        ChartColor.Muted => ThemeKeys.TextTertiary,
        ChartColor.Track => ThemeKeys.Border,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
    };

    /// <summary>The brush of a chart color in the element's current theme.</summary>
    public static IBrush BrushOf(StyledElement element, ChartColor color) => BrushOf(element, KeyOf(color));

    public static IBrush BrushOf(StyledElement element, string key) =>
        element.TryFindResource(key, element.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : Brushes.Gray;

    /// <returns>What the tooltip says at this point of the control, or null for no tooltip.</returns>
    public abstract string? TooltipAt(Point point);

    public sealed override void Render(DrawingContext context)
    {
        // A transparent fill makes the whole area hit-testable, so the tooltip works between the bars too.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (HasData)
        {
            RenderChart(context, Bounds.Size);
        }
        else if (!string.IsNullOrEmpty(EmptyText))
        {
            var text = Label(EmptyText, 12, ThemeKeys.TextTertiary, Bounds.Width - 16);
            context.DrawText(text, new Point((Bounds.Width - text.Width) / 2, (Bounds.Height - text.Height) / 2));
        }
    }

    protected abstract void RenderChart(DrawingContext context, Size size);

    protected IBrush Brush(ChartColor color) => BrushOf(this, color);

    protected IBrush Brush(string key) => BrushOf(this, key);

    /// <param name="maxWidth">Cuts the text off with an ellipsis beyond this width.</param>
    protected FormattedText Label(
        string text,
        double size = 11,
        string key = ThemeKeys.TextTertiary,
        double maxWidth = double.PositiveInfinity,
        FontWeight weight = FontWeight.Normal)
    {
        var family = this.TryFindResource(ThemeKeys.UiFont, ActualThemeVariant, out var value) && value is FontFamily font
            ? font
            : FontFamily.Default;
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(family, FontStyle.Normal, weight),
            size,
            Brush(key));
        if (!double.IsPositiveInfinity(maxWidth))
        {
            formatted.MaxTextWidth = Math.Max(1, maxWidth);
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }

        return formatted;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var tip = HasData ? TooltipAt(e.GetPosition(this)) : null;
        ToolTip.SetTip(this, tip);
        ToolTip.SetIsOpen(this, tip is not null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ToolTip.SetIsOpen(this, false);
        ToolTip.SetTip(this, null);
    }
}
