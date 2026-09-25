using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Mcacc.App.Controls;
using Mcacc.App.Theme;

namespace Mcacc.App.Tray;

/// <summary>The images of the tray icon: the app icon on Windows, a template glyph in the macOS menu bar.</summary>
internal static class TrayIconImage
{
    /// <summary>44 px is the 22 pt of a menu bar item on a Retina display.</summary>
    internal const int TemplateSize = 44;

    private const string WindowsIcon = "avares://MCACC/Assets/Icon/mcacc.ico";

    public static WindowIcon Windows()
    {
        using var stream = AssetLoader.Open(new Uri(WindowsIcon));
        return new WindowIcon(stream);
    }

    /// <summary>
    /// Black on transparent, drawn from the bundled icon font, so no extra image has to be kept. macOS uses only its
    /// alpha and colors it to match a light or dark menu bar.
    /// </summary>
    public static Bitmap RenderTemplate()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(TemplateSize, TemplateSize), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            var glyph = new FormattedText(
                MIconGlyphs.Of(MIconKind.SwitchAccount),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily(McaccFontCollection.Icons)),
                TemplateSize - 4,
                Brushes.Black);
            context.DrawText(glyph, new Point((TemplateSize - glyph.Width) / 2, (TemplateSize - glyph.Height) / 2));
        }

        return bitmap;
    }

    public static WindowIcon Template() => new(RenderTemplate());
}
