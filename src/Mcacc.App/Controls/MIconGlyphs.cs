namespace Mcacc.App.Controls;

/// <summary>The codepoints of Material Icons Round, taken from the font's own .codepoints file.</summary>
public static class MIconGlyphs
{
    private static readonly Dictionary<MIconKind, string> Glyphs = new()
    {
        [MIconKind.Add] = "",
        [MIconKind.Settings] = "",
        [MIconKind.ArrowBack] = "",
        [MIconKind.MoreVert] = "",
        [MIconKind.Edit] = "",
        [MIconKind.Delete] = "",
        [MIconKind.Refresh] = "",
        [MIconKind.DesktopWindows] = "",
        [MIconKind.Terminal] = "",
        [MIconKind.FolderOpen] = "",
        [MIconKind.Check] = "",
        [MIconKind.Close] = "",
        [MIconKind.Warning] = "",
        [MIconKind.Info] = "",
        [MIconKind.SwapHoriz] = "",
        [MIconKind.Logout] = "",
        [MIconKind.Insights] = "",
    };

    /// <exception cref="ArgumentOutOfRangeException">The kind has no codepoint.</exception>
    public static string Of(MIconKind kind) => Glyphs.TryGetValue(kind, out var glyph)
        ? glyph
        : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No codepoint for this icon.");

    public static IReadOnlyCollection<MIconKind> Known => Glyphs.Keys;
}
