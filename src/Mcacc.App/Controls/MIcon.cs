using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mcacc.App.Theme;

namespace Mcacc.App.Controls;

/// <summary>One glyph of the embedded Material Icons Round font; a null <see cref="Kind"/> draws nothing.</summary>
public sealed class MIcon : TextBlock
{
    public static readonly StyledProperty<MIconKind?> KindProperty =
        AvaloniaProperty.Register<MIcon, MIconKind?>(nameof(Kind));

    public MIcon()
    {
        FontFamily = new FontFamily(McaccFontCollection.Icons);
        FontSize = 20;
        TextAlignment = TextAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
        TextWrapping = TextWrapping.NoWrap;
    }

    public MIconKind? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
        {
            Text = Kind is { } kind ? MIconGlyphs.Of(kind) : "";
        }
    }
}
