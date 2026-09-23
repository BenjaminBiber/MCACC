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

    public static IReadOnlyList<string> All =>
    [
        Bg, Surface, SurfaceHover, Border, BorderStrong, Text, TextSecondary, TextTertiary,
        Accent, AccentFill, AccentSubtle, Danger, Scrim,
    ];
}
