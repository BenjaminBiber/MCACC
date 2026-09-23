using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Mcacc.App.Controls;

/// <summary>One usage window as a thin bar: label, percentage and reset time. A null value reads as "—".</summary>
public sealed class UsageBar : TemplatedControl
{
    /// <summary>From here on the bar switches to the danger color.</summary>
    public const double WarningThreshold = 90;

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<UsageBar, string?>(nameof(Label));

    public static readonly StyledProperty<double?> ValueProperty =
        AvaloniaProperty.Register<UsageBar, double?>(nameof(Value));

    public static readonly StyledProperty<string?> ResetTextProperty =
        AvaloniaProperty.Register<UsageBar, string?>(nameof(ResetText));

    public static readonly DirectProperty<UsageBar, bool> IsWarningProperty =
        AvaloniaProperty.RegisterDirect<UsageBar, bool>(nameof(IsWarning), bar => bar.IsWarning);

    public static readonly DirectProperty<UsageBar, string> TextProperty =
        AvaloniaProperty.RegisterDirect<UsageBar, string>(nameof(Text), bar => bar.Text);

    public static readonly DirectProperty<UsageBar, double> FractionProperty =
        AvaloniaProperty.RegisterDirect<UsageBar, double>(nameof(Fraction), bar => bar.Fraction);

    private bool _isWarning;
    private string _text = "";
    private double _fraction;

    public UsageBar() => Refresh();

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Percent used, 0-100; null when the plan reports no limit.</summary>
    public double? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? ResetText
    {
        get => GetValue(ResetTextProperty);
        set => SetValue(ResetTextProperty, value);
    }

    public bool IsWarning
    {
        get => _isWarning;
        private set => SetAndRaise(IsWarningProperty, ref _isWarning, value);
    }

    /// <summary>The whole line, e.g. <c>5h  42 % · Reset 14:30</c>.</summary>
    public string Text
    {
        get => _text;
        private set => SetAndRaise(TextProperty, ref _text, value);
    }

    /// <summary>The filled part of the track, 0 to 1.</summary>
    public double Fraction
    {
        get => _fraction;
        private set => SetAndRaise(FractionProperty, ref _fraction, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == LabelProperty || change.Property == ResetTextProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var value = Value;
        IsWarning = value >= WarningThreshold;
        Fraction = value is null ? 0 : Math.Clamp(value.Value, 0, 100) / 100;
        var percent = value is null
            ? "—"
            : string.Create(CultureInfo.CurrentCulture, $"{Math.Round(value.Value)} %");
        Text = string.IsNullOrEmpty(ResetText) ? $"{Label}  {percent}" : $"{Label}  {percent} · {ResetText}";
    }
}
