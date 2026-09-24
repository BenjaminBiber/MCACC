using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Mcacc.App.Controls;
using Mcacc.App.Theme;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Theme;

public class ThemeTests
{
    private static readonly ThemeVariant[] Variants = [ThemeVariant.Light, ThemeVariant.Dark];

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Every_token_resolves_to_a_brush_in_both_variants(string variantName)
    {
        var variant = Variants.Single(v => v.Key.ToString() == variantName);
        var application = Application.Current.ShouldNotBeNull();

        foreach (var key in ThemeKeys.All)
        {
            application.TryGetResource(key, variant, out var value).ShouldBeTrue($"'{key}' is missing.");
            value.ShouldBeOfType<SolidColorBrush>($"'{key}' is not a brush.");
        }
    }

    [AvaloniaFact]
    public void The_accent_is_claude_orange_in_both_variants()
    {
        var application = Application.Current.ShouldNotBeNull();

        foreach (var variant in Variants)
        {
            application.TryGetResource(ThemeKeys.Accent, variant, out var value).ShouldBeTrue();
            value.ShouldBeOfType<SolidColorBrush>().Color.ShouldBe(Color.Parse("#D97757"));
        }
    }

    [AvaloniaFact]
    public void The_background_differs_between_light_and_dark()
    {
        var application = Application.Current.ShouldNotBeNull();

        application.TryGetResource(ThemeKeys.Bg, ThemeVariant.Light, out var light).ShouldBeTrue();
        application.TryGetResource(ThemeKeys.Bg, ThemeVariant.Dark, out var dark).ShouldBeTrue();

        light.ShouldBeOfType<SolidColorBrush>().Color.ShouldBe(Color.Parse("#FAF9F5"));
        dark.ShouldBeOfType<SolidColorBrush>().Color.ShouldBe(Color.Parse("#262624"));
    }

    [AvaloniaTheory]
    [InlineData(McaccFontCollection.Serif)]
    [InlineData(McaccFontCollection.SerifDisplay)]
    [InlineData(McaccFontCollection.Icons)]
    public void The_embedded_font_families_resolve(string family)
    {
        FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var typeface).ShouldBeTrue(family);

        typeface.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Every_icon_kind_has_a_codepoint()
    {
        foreach (var kind in Enum.GetValues<MIconKind>())
        {
            MIconGlyphs.Of(kind).Length.ShouldBe(1);
        }

        MIconGlyphs.Known.Count.ShouldBe(Enum.GetValues<MIconKind>().Length);
    }

    [AvaloniaFact]
    public void Every_icon_codepoint_is_in_the_private_use_area()
    {
        foreach (var kind in Enum.GetValues<MIconKind>())
        {
            MIconGlyphs.Of(kind)[0].ShouldBeInRange('', '');
        }
    }

    [AvaloniaFact]
    public void Icon_codepoints_are_unique()
    {
        var glyphs = Enum.GetValues<MIconKind>().Select(MIconGlyphs.Of).ToList();

        glyphs.Distinct().Count().ShouldBe(glyphs.Count);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Every_chart_color_resolves_to_a_brush(string variantName)
    {
        var variant = Variants.Single(v => v.Key.ToString() == variantName);
        var application = Application.Current.ShouldNotBeNull();

        foreach (var color in Enum.GetValues<ChartColor>())
        {
            application.TryGetResource(ChartControl.KeyOf(color), variant, out var value).ShouldBeTrue(color.ToString());
            value.ShouldBeOfType<SolidColorBrush>();
        }
    }

    [AvaloniaFact]
    public void The_six_series_colors_differ_from_each_other()
    {
        var application = Application.Current.ShouldNotBeNull();

        foreach (var variant in Variants)
        {
            var colors = ThemeKeys.Series
                .Select(key => application.TryGetResource(key, variant, out var value) ? ((SolidColorBrush)value!).Color : default)
                .ToList();
            colors.Distinct().Count().ShouldBe(6);
        }
    }
}
