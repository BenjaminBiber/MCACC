using Avalonia.Media.Fonts;

namespace Mcacc.App.Theme;

/// <summary>Makes the fonts under <c>Assets/Fonts</c> addressable as <c>fonts:Mcacc#&lt;family&gt;</c>.</summary>
public sealed class McaccFontCollection() : EmbeddedFontCollection(new Uri(CollectionKey), new Uri(Source))
{
    public const string CollectionKey = "fonts:Mcacc";
    public const string Source = "avares://MCACC/Assets/Fonts";

    public const string SerifDisplay = $"{CollectionKey}#Source Serif 4 Display";
    public const string Serif = $"{CollectionKey}#Source Serif 4";
    public const string Icons = $"{CollectionKey}#Material Icons Round";
}
