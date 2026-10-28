using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Mcacc.App.Controls;

public enum LaunchTileVariant
{
    Account,
    Add,
    Target,
}

/// <summary>The one clickable card the whole UI is built from: an account, a launch target, or the add tile.</summary>
public sealed class LaunchTile : Button
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LaunchTile, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<LaunchTile, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<LaunchTile, string?>(nameof(Badge));

    public static readonly StyledProperty<string?> ProviderBadgeProperty =
        AvaloniaProperty.Register<LaunchTile, string?>(nameof(ProviderBadge));

    public static readonly StyledProperty<string?> AvatarTextProperty =
        AvaloniaProperty.Register<LaunchTile, string?>(nameof(AvatarText));

    public static readonly StyledProperty<IBrush?> AvatarBrushProperty =
        AvaloniaProperty.Register<LaunchTile, IBrush?>(nameof(AvatarBrush));

    public static readonly StyledProperty<MIconKind?> IconKindProperty =
        AvaloniaProperty.Register<LaunchTile, MIconKind?>(nameof(IconKind));

    public static readonly StyledProperty<LaunchTileVariant> VariantProperty =
        AvaloniaProperty.Register<LaunchTile, LaunchTileVariant>(nameof(Variant));

    public static readonly StyledProperty<bool> IsHighlightedProperty =
        AvaloniaProperty.Register<LaunchTile, bool>(nameof(IsHighlighted));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<LaunchTile, object?>(nameof(Footer));

    public static readonly StyledProperty<Control?> OverlayProperty =
        AvaloniaProperty.Register<LaunchTile, Control?>(nameof(Overlay));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>A short state, e.g. "Läuft"; null hides the badge.</summary>
    public string? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <summary>Which service the account belongs to; empty where that needs no saying.</summary>
    public string? ProviderBadge
    {
        get => GetValue(ProviderBadgeProperty);
        set => SetValue(ProviderBadgeProperty, value);
    }

    public string? AvatarText
    {
        get => GetValue(AvatarTextProperty);
        set => SetValue(AvatarTextProperty, value);
    }

    public IBrush? AvatarBrush
    {
        get => GetValue(AvatarBrushProperty);
        set => SetValue(AvatarBrushProperty, value);
    }

    public MIconKind? IconKind
    {
        get => GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    public LaunchTileVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public bool IsHighlighted
    {
        get => GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    /// <summary>Extra content below the subtitle, used for the usage bars.</summary>
    [Content]
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>A control in the top right corner, used for the context menu button.</summary>
    public Control? Overlay
    {
        get => GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title ?? "");
        }
    }
}
