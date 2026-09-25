using Mcacc.App.Controls;
using Mcacc.App.Localization;
using Mcacc.Core.Usage;

namespace Mcacc.App.ViewModels;

public sealed record InfoFieldViewModel(string Label, string Value);

/// <param name="Caption">A note under the value, e.g. that the cost is an estimate.</param>
public sealed record KpiViewModel(string Label, string Value, string? Caption = null);

/// <param name="Dashed">The entry stands for a dashed line rather than a color.</param>
public sealed record LegendItemViewModel(string Label, ChartColor Color, string? Value = null, bool Dashed = false);

public sealed record ModelRowViewModel(string Name, string Tokens, string Share, string Cost, ChartColor Color);

/// <summary>One limit window as a ring: the used part in color, the rest as track.</summary>
public sealed record LimitRingViewModel(string Label, string ValueText, string? ResetText, IReadOnlyList<ChartSlice> Slices)
{
    public static LimitRingViewModel From(string label, UsageWindow window)
    {
        var value = Math.Clamp(window.Utilization ?? 0, 0, 100);
        var color = value >= UsageBar.WarningThreshold ? ChartColor.Danger : ChartColor.Accent;
        var text = Texts.Percent(value);
        return new LimitRingViewModel(
            label,
            text,
            window.ResetsAt is { } reset ? Texts.UsageReset(reset) : null,
            [new ChartSlice(label, value, color, $"{label}: {text}"), new ChartSlice(string.Empty, 100 - value, ChartColor.Track)]);
    }
}
