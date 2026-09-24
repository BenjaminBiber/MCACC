namespace Mcacc.App.Theme;

/// <summary>The keys the theme files define, so code and tests do not spell them twice.</summary>
public static class ThemeKeys
{
    public const string Bg = "Mcacc.Bg";
    public const string Surface = "Mcacc.Surface";
    public const string SurfaceHover = "Mcacc.SurfaceHover";
    public const string Border = "Mcacc.Border";
    public const string BorderStrong = "Mcacc.BorderStrong";
    public const string Text = "Mcacc.Text";
    public const string TextSecondary = "Mcacc.TextSecondary";
    public const string TextTertiary = "Mcacc.TextTertiary";
    public const string Accent = "Mcacc.Accent";
    public const string AccentFill = "Mcacc.AccentFill";
    public const string AccentSubtle = "Mcacc.AccentSubtle";
    public const string Danger = "Mcacc.Danger";
    public const string Scrim = "Mcacc.Scrim";
    public const string Series1 = "Mcacc.Series1";
    public const string Series2 = "Mcacc.Series2";
    public const string Series3 = "Mcacc.Series3";
    public const string Series4 = "Mcacc.Series4";
    public const string Series5 = "Mcacc.Series5";
    public const string Series6 = "Mcacc.Series6";

    /// <summary>The font family of the UI text; a font, so it is not in <see cref="All"/>.</summary>
    public const string UiFont = "Mcacc.Ui";

    /// <summary>The chart colors in the order the series use them.</summary>
    public static IReadOnlyList<string> Series => [Series1, Series2, Series3, Series4, Series5, Series6];

    public static IReadOnlyList<string> All =>
    [
        Bg, Surface, SurfaceHover, Border, BorderStrong, Text, TextSecondary, TextTertiary,
        Accent, AccentFill, AccentSubtle, Danger, Scrim,
        Series1, Series2, Series3, Series4, Series5, Series6,
    ];
}
