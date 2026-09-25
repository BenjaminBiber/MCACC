# Lücken im Limit-Verlauf schätzen, Autostart bei der Anmeldung – Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Der Limit-Verlauf füllt Lücken gestrichelt (mit Reset-Treppe), und MCACC startet bei der Anmeldung versteckt im Infobereich, damit der Verlauf ohne manuellen Start weiterwächst.

**Architecture:** Die Schätzung ist eine reine Funktion in Core (`UsageGapEstimate`); das ViewModel der Info-Seite legt damit gestrichelte Polylinien an `LineSeries.Estimated`, die `LineChart` zeichnet. Der Autostart ist eine Plattform-Schnittstelle `ILoginItem` (Windows: Run-Wert in HKCU, macOS: LaunchAgent-plist), die `LoginItemSync` beim Start und beim Umschalten mit `AppSettings.LaunchAtLogin` abgleicht; `--hidden` startet ohne Fenster.

**Tech Stack:** .NET 10, C# latest, Avalonia 11, CommunityToolkit.Mvvm, xunit v3, Shouldly.

**Spec:** `docs/superpowers/specs/2026-09-25-history-gaps-autostart-design.md` · **Tickets:** MCA-0013 (Tasks 1–3), MCA-0014 (Tasks 4–8), Task 9 für beide.

## Global Constraints

- `TreatWarningsAsErrors` ist an: `dotnet build MCACC.slnx` muss ohne Warnung durchlaufen.
- Kommentare und XML-Doku im Code auf **Englisch**, im Stil des Codes (kurz, erklärt das Warum). UI-Texte auf **Deutsch** in `src/Mcacc.App/Localization/Texts*.cs`.
- File-scoped namespaces, `var` wo möglich, LF-Zeilenenden, 4 Spaces.
- Tests: xunit v3 (`TestContext.Current.CancellationToken`), Shouldly. Testnamen als englischer Satz mit Unterstrichen.
- Fensterlängen: 5 h (`UsageGapEstimate.FiveHour`) und 7 Tage (`UsageGapEstimate.SevenDay`).
- Strichmuster der Schätzung: `DashStyle([4, 3], 0)`, 1,5 px, Farbe der Reihe.
- Run-Wert: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, Name `MCACC`, Wert `"<exe>" --hidden`.
- LaunchAgent: `~/Library/LaunchAgents/de.benjaminbiber.mcacc.plist`, Label `de.benjaminbiber.mcacc`.
- Der Abgleich des Autostarts ist in Debug-Builds und bei gesetztem `MCACC_HOME` aus.
- Testbefehl für alles: `dotnet test MCACC.slnx` (aus dem Repo-Root).
- Jeder Commit endet mit der Zeile `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

---

### Task 1: `UsageGapEstimate` in Core

**Files:**
- Create: `src/Mcacc.Core/Usage/UsageGapEstimate.cs`
- Test: `tests/Mcacc.Core.Tests/Usage/UsageGapEstimateTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct UsageValue(DateTimeOffset At, double Value);` (namespace `Mcacc.Core.Usage`)
  - `public static class UsageGapEstimate` mit
    - `public static readonly TimeSpan FiveHour` (5 h), `public static readonly TimeSpan SevenDay` (7 d)
    - `public static IReadOnlyList<UsageValue> Bridge(UsageValue a, DateTimeOffset? aResetsAt, UsageValue b, DateTimeOffset? bResetsAt, TimeSpan window)`
    - `public static IReadOnlyList<UsageValue> From(IReadOnlyList<UsageValue> line, DateTimeOffset start)`

- [ ] **Step 1: Write the failing tests**

`tests/Mcacc.Core.Tests/Usage/UsageGapEstimateTests.cs`:

```csharp
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public class UsageGapEstimateTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour, int minute) => Day.AddHours(hour).AddMinutes(minute);

    private static UsageValue V(int hour, int minute, double value) => new(At(hour, minute), value);

    [Fact]
    public void A_reset_in_the_gap_holds_the_value_drops_to_zero_and_rises_from_the_start_of_the_next_window()
    {
        // The history of 2026-09-24 in UTC: 97 % at 10:08, the window reset at 10:40, the next one began at 11:10.
        var line = UsageGapEstimate.Bridge(
            V(10, 8, 97), At(10, 40),
            V(11, 15, 5), At(16, 10),
            UsageGapEstimate.FiveHour);

        line.ShouldBe([V(10, 8, 97), V(10, 40, 97), V(10, 40, 0), V(11, 10, 0), V(11, 15, 5)]);
    }

    [Fact]
    public void Without_a_reset_in_the_gap_the_line_is_straight()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 20), At(12, 0), V(10, 0, 35), At(12, 0), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 20), V(10, 0, 35)]);
    }

    [Fact]
    public void Without_the_next_reset_the_line_rises_right_from_the_reset()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 60), At(9, 0), V(11, 0, 10), null, UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 60), V(9, 0, 60), V(9, 0, 0), V(11, 0, 10)]);
    }

    [Fact]
    public void Without_an_active_window_the_value_holds_until_the_next_window_begins()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 0), null, V(12, 0, 4), At(16, 30), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 0), V(11, 30, 0), V(12, 0, 4)]);
    }

    [Fact]
    public void Without_any_reset_the_line_is_straight()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 0), null, V(12, 0, 4), null, UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 0), V(12, 0, 4)]);
    }

    [Fact]
    public void A_next_window_that_seems_to_begin_before_the_reset_begins_at_the_reset()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 50), At(9, 0), V(10, 0, 5), At(13, 0), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 50), V(9, 0, 50), V(9, 0, 0), V(10, 0, 5)]);
    }

    [Fact]
    public void A_next_window_that_seems_to_begin_after_the_next_point_begins_there()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 50), At(9, 0), V(10, 0, 5), At(16, 0), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 50), V(9, 0, 50), V(9, 0, 0), V(10, 0, 0), V(10, 0, 5)]);
    }

    [Fact]
    public void A_zero_before_the_reset_leaves_no_empty_corners()
    {
        var line = UsageGapEstimate.Bridge(V(8, 0, 0), At(9, 0), V(12, 0, 3), At(16, 30), UsageGapEstimate.FiveHour);

        line.ShouldBe([V(8, 0, 0), V(9, 0, 0), V(11, 30, 0), V(12, 0, 3)]);
    }

    [Fact]
    public void The_week_window_is_seven_days_long()
    {
        // The next week began at 07:00 the day after and resets seven days later; with five hours it would begin after b.
        var begin = At(7, 0).AddDays(1);
        var b = new UsageValue(At(8, 0).AddDays(1), 2);

        var line = UsageGapEstimate.Bridge(V(15, 0, 38), At(16, 0), b, begin.AddDays(7), UsageGapEstimate.SevenDay);

        line.ShouldBe([V(15, 0, 38), V(16, 0, 38), V(16, 0, 0), new UsageValue(begin, 0), b]);
    }

    [Fact]
    public void From_cuts_the_line_at_the_start_and_interpolates_the_crossing()
    {
        UsageValue[] line = [V(8, 0, 40), V(10, 0, 60), V(12, 0, 0)];

        UsageGapEstimate.From(line, At(9, 0)).ShouldBe([V(9, 0, 50), V(10, 0, 60), V(12, 0, 0)]);
    }

    [Fact]
    public void From_keeps_a_line_that_begins_after_the_start()
    {
        UsageValue[] line = [V(10, 0, 60), V(12, 0, 0)];

        UsageGapEstimate.From(line, At(9, 0)).ShouldBe(line);
    }

    [Fact]
    public void From_drops_a_line_that_ends_before_the_start()
    {
        UsageValue[] line = [V(6, 0, 60), V(8, 0, 0)];

        UsageGapEstimate.From(line, At(9, 0)).ShouldBeEmpty();
    }

    [Fact]
    public void From_passes_a_vertical_edge_before_the_start_by_its_lower_end()
    {
        UsageValue[] line = [V(6, 0, 60), V(8, 0, 60), V(8, 0, 0), V(12, 0, 0), V(13, 0, 8)];

        UsageGapEstimate.From(line, At(9, 0)).ShouldBe([V(9, 0, 0), V(12, 0, 0), V(13, 0, 8)]);
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~UsageGapEstimateTests"`
Expected: Build error, `UsageGapEstimate` / `UsageValue` do not exist.

- [ ] **Step 3: Implement**

`src/Mcacc.Core/Usage/UsageGapEstimate.cs`:

```csharp
namespace Mcacc.Core.Usage;

/// <summary>A limit value at one moment, measured or guessed.</summary>
public readonly record struct UsageValue(DateTimeOffset At, double Value);

/// <summary>
/// Guesses a limit line across a stretch without history points, e.g. while MCACC was not running. Within a window the
/// utilization only grows; at the reset it drops to 0, and the next window begins with the next use, which the reset
/// time of the next point tells: it lies one window length after that beginning.
/// </summary>
public static class UsageGapEstimate
{
    public static readonly TimeSpan FiveHour = TimeSpan.FromHours(5);

    public static readonly TimeSpan SevenDay = TimeSpan.FromDays(7);

    /// <param name="window">The length of the window the values belong to: <see cref="FiveHour"/> or <see cref="SevenDay"/>.</param>
    /// <returns>The corners of the guessed line from <paramref name="a"/> to <paramref name="b"/>, both included.</returns>
    public static IReadOnlyList<UsageValue> Bridge(
        UsageValue a,
        DateTimeOffset? aResetsAt,
        UsageValue b,
        DateTimeOffset? bResetsAt,
        TimeSpan window)
    {
        var nextStart = bResetsAt - window;
        var corners = new List<UsageValue> { a };
        if (aResetsAt is { } reset)
        {
            if (reset > b.At)
            {
                // Still the window of a: the value rose from a to b at some unknown time.
                return [a, b];
            }

            var resetAt = Clamp(reset, a.At, b.At);
            var beginAt = nextStart is { } begin ? Clamp(begin, resetAt, b.At) : resetAt;
            corners.Add(new UsageValue(resetAt, a.Value));
            corners.Add(new UsageValue(resetAt, 0));
            corners.Add(new UsageValue(beginAt, 0));
        }
        else if (nextStart is { } begin)
        {
            // No window was running at a, so nothing was used until the next one began.
            corners.Add(new UsageValue(Clamp(begin, a.At, b.At), a.Value));
        }

        corners.Add(b);
        return WithoutRepeats(corners);
    }

    /// <summary>The part of <paramref name="line"/> from <paramref name="start"/> on; the crossing is interpolated.</summary>
    public static IReadOnlyList<UsageValue> From(IReadOnlyList<UsageValue> line, DateTimeOffset start)
    {
        var kept = new List<UsageValue>();
        for (var i = 0; i < line.Count; i++)
        {
            if (line[i].At < start)
            {
                continue;
            }

            if (kept.Count == 0 && i > 0 && line[i].At > start)
            {
                kept.Add(Interpolate(line[i - 1], line[i], start));
            }

            kept.Add(line[i]);
        }

        return kept;
    }

    private static UsageValue Interpolate(UsageValue before, UsageValue after, DateTimeOffset at)
    {
        var share = (at - before.At) / (after.At - before.At);
        return new UsageValue(at, before.Value + (after.Value - before.Value) * share);
    }

    private static DateTimeOffset Clamp(DateTimeOffset value, DateTimeOffset min, DateTimeOffset max) =>
        value < min ? min : value > max ? max : value;

    /// <summary>A corner equal to the one before it would only draw a line of no length.</summary>
    private static List<UsageValue> WithoutRepeats(List<UsageValue> corners)
    {
        var kept = new List<UsageValue>(corners.Count);
        foreach (var corner in corners)
        {
            if (kept.Count == 0 || kept[^1] != corner)
            {
                kept.Add(corner);
            }
        }

        return kept;
    }
}
```

Hinweis zu `From`: Liegt ein Eckpunkt genau auf `start`, wird er ohne Interpolation übernommen (`line[i].At > start` ist dann falsch).

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~UsageGapEstimateTests"`
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Mcacc.Core/Usage/UsageGapEstimate.cs tests/Mcacc.Core.Tests/Usage/UsageGapEstimateTests.cs
git commit -m "feat(core): estimate the limit line across a gap in the history"
```

---

### Task 2: Info-Seite legt Brücken an (ViewModel)

**Files:**
- Modify: `src/Mcacc.App/Controls/ChartData.cs` (`LineSeries`)
- Modify: `src/Mcacc.App/ViewModels/AccountInfoItems.cs:12` (`LegendItemViewModel`)
- Modify: `src/Mcacc.App/ViewModels/AccountInfoViewModel.cs` (`HistoryLegend`, `LoadHistory`, `Line`)
- Modify: `src/Mcacc.App/Localization/Texts.Info.cs` (neuer Text)
- Test: `tests/Mcacc.App.Tests/ViewModels/AccountInfoViewModelTests.cs`

**Interfaces:**
- Consumes: `UsageValue`, `UsageGapEstimate.Bridge/From/FiveHour/SevenDay` (Task 1).
- Produces:
  - `LineSeries.Estimated`: `IReadOnlyList<IReadOnlyList<ChartPoint>>`, init-only, Standard `[]`.
  - `LegendItemViewModel(string Label, ChartColor Color, string? Value = null, bool Dashed = false)`.
  - `AccountInfoViewModel.HistoryLegend` wird eine beobachtbare Eigenschaft (`[ObservableProperty]`, `private set`).
  - `Texts.InfoHistoryEstimated = "nicht gemessen"`.

- [ ] **Step 1: Write the failing tests**

In `AccountInfoViewModelTests.cs` direkt unter dem vorhandenen Helfer `Point(double hoursAgo, double? fiveHour, double? sevenDay)` (Zeile 38, ohne Resets) eine Überladung mit dem 5-h-Reset ergänzen. Die beiden unterscheiden sich in der Parameterzahl; mit drei Argumenten gilt weiter der alte Helfer:

```csharp
    private static UsageHistoryPoint Point(double hoursAgo, double fiveHour, double? fiveHourResetsInHours, double sevenDay)
    {
        var at = DateTimeOffset.Now.AddHours(-hoursAgo);
        return new UsageHistoryPoint(
            at, fiveHour, fiveHourResetsInHours is { } hours ? at.AddHours(hours) : null, sevenDay, null);
    }
```

Tests unter dem Abschnitt `// History` ergänzen. Die Punkte in Zeitreihenfolge anlegen: `FakeUsageProvider.GetHistory` filtert nur, sortiert nicht.

```csharp
    [Fact]
    public void A_gap_wider_than_the_day_gap_gets_a_dashed_bridge()
    {
        _services.Usage.History["privat"] = [Point(10, 20, 10), Point(2, 30, 10)];

        var page = Page();

        var bridge = page.HistorySeries[1].Estimated.ShouldHaveSingleItem();
        bridge.Select(p => p.Y).ShouldBe([10, 10]);
        bridge[0].X.ShouldBe(14, tolerance: 0.01);
        bridge[^1].X.ShouldBe(22, tolerance: 0.01);
        bridge.ShouldAllBe(p => p.Tooltip == null);
    }

    [Fact]
    public void A_reset_in_the_gap_draws_the_stair_with_the_five_hour_resets()
    {
        // 97 % four hours ago, reset half an hour later; 5 % now, in a window that began 20 minutes ago.
        _services.Usage.History["privat"] = [Point(4, 97, 0.5, 30), Point(0, 5, 4 + 40 / 60d, 31)];

        var page = Page();

        page.HistorySeries[0].Estimated.ShouldHaveSingleItem().Select(p => p.Y).ShouldBe([97, 97, 0, 0, 5]);
    }

    [Fact]
    public void Points_closer_than_the_gap_get_no_bridge()
    {
        _services.Usage.History["privat"] = [Point(1, 20, 10), Point(0.9, 21, 10)];

        var page = Page();

        page.HistorySeries.ShouldAllBe(series => series.Estimated.Count == 0);
        page.HistoryLegend.Select(item => item.Label).ShouldBe([Texts.UsageFiveHour, Texts.UsageSevenDay]);
    }

    [Fact]
    public void A_bridge_comes_in_from_the_left_edge_but_the_older_point_is_not_drawn()
    {
        _services.Usage.History["privat"] = [Point(30, 50, null, 5), Point(2, 60, null, 5)];

        var page = Page();

        page.HistorySeries[0].Points.ShouldHaveSingleItem().Y.ShouldBe(60);
        var bridge = page.HistorySeries[0].Estimated.ShouldHaveSingleItem();
        bridge[0].X.ShouldBe(0, tolerance: 0.01);
        bridge[0].Y.ShouldBe(50 + 10 * (6 / 28d), tolerance: 0.1);
        bridge[^1].X.ShouldBe(22, tolerance: 0.01);
    }

    [Fact]
    public void Only_a_bridge_puts_the_estimate_into_the_legend()
    {
        _services.Usage.History["privat"] = [Point(10, 20, 10), Point(2, 30, 10)];

        var page = Page();

        page.HistoryLegend.Select(item => item.Label)
            .ShouldBe([Texts.UsageFiveHour, Texts.UsageSevenDay, Texts.InfoHistoryEstimated]);
        page.HistoryLegend[^1].Dashed.ShouldBeTrue();
    }

    [Fact]
    public void History_only_before_the_range_shows_no_series()
    {
        _services.Usage.History["privat"] = [Point(30, 50, 10), Point(28, 60, 10)];

        Page().HistorySeries.ShouldBeEmpty();
    }
```

Rechnung zum ersten Test: `Point(10, 20, 10)` ist der alte Helfer (5h = 20, Woche = 10, keine Resets). Ohne Resets sind beide Brücken gerade; die der Woche läuft von X = 14 (vor 10 h) bis X = 22 (vor 2 h) auf 10.

Rechnung zur Treppe: Der Reset von a liegt 3,5 h zurück, das Fenster von b begann 4 h 40 min vor seinem Reset in 20 min, also vor 20 min. Die Eckpunkte sind 97 → 97 → 0 → 0 → 5.

Rechnung zum linken Rand: a liegt 30 h zurück (X = −6), b 2 h zurück (X = 22); der Schnitt bei X = 0 liegt 6/28 des Wegs von 50 nach 60.

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~AccountInfoViewModelTests"`
Expected: Build error (`Estimated`, `Dashed`, `InfoHistoryEstimated` fehlen).

- [ ] **Step 3: Implement**

`src/Mcacc.App/Controls/ChartData.cs` – `LineSeries` ersetzen:

```csharp
public sealed record LineSeries(string Name, ChartColor Color, IReadOnlyList<ChartPoint> Points)
{
    /// <summary>Guessed stretches where nothing was measured, drawn dashed; they carry no tooltips.</summary>
    public IReadOnlyList<IReadOnlyList<ChartPoint>> Estimated { get; init; } = [];
}
```

`src/Mcacc.App/ViewModels/AccountInfoItems.cs:12`:

```csharp
/// <param name="Dashed">The entry stands for a dashed line rather than a color.</param>
public sealed record LegendItemViewModel(string Label, ChartColor Color, string? Value = null, bool Dashed = false);
```

`src/Mcacc.App/Localization/Texts.Info.cs` unter `InfoNoHistory`:

```csharp
    public const string InfoHistoryEstimated = "nicht gemessen";
```

`src/Mcacc.App/ViewModels/AccountInfoViewModel.cs`:

1. Die feste Eigenschaft `HistoryLegend` (`public IReadOnlyList<LegendItemViewModel> HistoryLegend { get; } = [ … ];`) ersetzen durch:

```csharp
    private static readonly IReadOnlyList<LegendItemViewModel> MeasuredLegend =
    [
        new(Texts.UsageFiveHour, ChartColor.Series1),
        new(Texts.UsageSevenDay, ChartColor.Series2),
    ];

    private static readonly LegendItemViewModel EstimatedLegend =
        new(Texts.InfoHistoryEstimated, ChartColor.Muted, Dashed: true);

    /// <summary>The two lines, and the dashed estimate once the history has a gap.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LegendItemViewModel> HistoryLegend { get; private set; } = MeasuredLegend;
```

(Die zwei statischen Felder oben in der Klasse zu den übrigen Konstanten stellen, die Eigenschaft an die Stelle der alten.)

2. `LoadHistory` und `Line` ersetzen:

```csharp
    private void LoadHistory()
    {
        var span = IsHistoryWeek ? TimeSpan.FromDays(7) : TimeSpan.FromHours(24);
        var start = DateTimeOffset.Now - span;
        // One span further back: a point before the start lets a bridge come in from the left edge.
        var points = _services.Usage.GetHistory(Account, start - span);
        var gap = IsHistoryWeek ? HistoryWeekGap : HistoryDayGap;
        HistoryMax = span.TotalHours;
        HistoryGap = gap;
        HistoryTicks = TicksFor(start, span, IsHistoryWeek, TimeZoneInfo.Local);
        HistorySeries = points.Any(point => point.At >= start)
            ?
            [
                Line(Texts.UsageFiveHour, ChartColor.Series1, points, start, gap, UsageGapEstimate.FiveHour,
                    point => point.FiveHour, point => point.FiveHourResetsAt),
                Line(Texts.UsageSevenDay, ChartColor.Series2, points, start, gap, UsageGapEstimate.SevenDay,
                    point => point.SevenDay, point => point.SevenDayResetsAt),
            ]
            : [];
        HistoryLegend = HistorySeries.Any(series => series.Estimated.Count > 0)
            ? [.. MeasuredLegend, EstimatedLegend]
            : MeasuredLegend;
    }
```

```csharp
    /// <summary>
    /// The measured points from <paramref name="start"/> on, and a dashed bridge over each gap wider than
    /// <paramref name="gap"/> hours; a point before the start only anchors a bridge from the left edge.
    /// </summary>
    private static LineSeries Line(
        string name,
        ChartColor color,
        IReadOnlyList<UsageHistoryPoint> points,
        DateTimeOffset start,
        double gap,
        TimeSpan window,
        Func<UsageHistoryPoint, double?> value,
        Func<UsageHistoryPoint, DateTimeOffset?> resetsAt)
    {
        var valued = points.Where(point => value(point) is not null).ToList();
        var bridges = new List<IReadOnlyList<ChartPoint>>();
        for (var i = 1; i < valued.Count; i++)
        {
            var (a, b) = (valued[i - 1], valued[i]);
            if (b.At < start || (b.At - a.At).TotalHours <= gap)
            {
                continue;
            }

            var bridge = UsageGapEstimate.From(
                UsageGapEstimate.Bridge(
                    new UsageValue(a.At, value(a)!.Value), resetsAt(a),
                    new UsageValue(b.At, value(b)!.Value), resetsAt(b),
                    window),
                start);
            if (bridge.Count >= 2)
            {
                bridges.Add([.. bridge.Select(corner => new ChartPoint((corner.At - start).TotalHours, corner.Value))]);
            }
        }

        var measured = valued
            .Where(point => point.At >= start)
            .Select(point => new ChartPoint(
                (point.At - start).TotalHours,
                value(point)!.Value,
                Texts.HistoryTooltip(point.At, point.FiveHour, point.SevenDay)))
            .ToList();
        return new LineSeries(name, color, measured) { Estimated = bridges };
    }
```

Die Punkte liefert `GetHistory` bereits zeitlich sortiert (`IUsageProvider`-Vertrag); der Fake in den Tests sortiert nicht, die Tests legen die Punkte deshalb in Zeitreihenfolge an.

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~AccountInfoViewModelTests"`
Expected: all PASS, auch die vorhandenen History-Tests (`The_history_shows_the_last_24_hours_first` etc.).

- [ ] **Step 5: Commit**

```bash
git add src/Mcacc.App/Controls/ChartData.cs src/Mcacc.App/ViewModels/AccountInfoItems.cs src/Mcacc.App/ViewModels/AccountInfoViewModel.cs src/Mcacc.App/Localization/Texts.Info.cs tests/Mcacc.App.Tests/ViewModels/AccountInfoViewModelTests.cs
git commit -m "feat(app): bridge the gaps of the limit history with an estimate"
```

---

### Task 3: Gestrichelt zeichnen (LineChart, Legende, Screenshot)

**Files:**
- Modify: `src/Mcacc.App/Controls/LineChart.cs` (`DrawSeries`, `DrawRun`, Klassen-Doku)
- Modify: `src/Mcacc.App/Controls/ChartSwatch.cs` (`Dashed`)
- Modify: `src/Mcacc.App/Views/AccountInfoView.axaml:9-16` (Legenden-Template)
- Modify: `src/Mcacc.App/Localization/Texts.Info.cs:42` (`InfoNoHistory`)
- Test: `tests/Mcacc.App.Tests/Views/ScreenshotTests.cs`

**Interfaces:**
- Consumes: `LineSeries.Estimated`, `LegendItemViewModel.Dashed` (Task 2).
- Produces: `ChartSwatch.Dashed` (`StyledProperty<bool>`).

- [ ] **Step 1: LineChart zeichnet `Estimated`**

In `LineChart.cs` die Klassen-Doku ergänzen:

```csharp
/// <summary>
/// Lines over a value axis from <see cref="Minimum"/> to <see cref="Maximum"/>, with an optional dashed threshold.
/// A line breaks where two points lie further apart than <see cref="MaxGap"/>, e.g. while the app was closed; a
/// series may bridge such a gap with a dashed estimate of its own (<see cref="LineSeries.Estimated"/>).
/// </summary>
```

`DrawSeries` und `DrawRun` ersetzen:

```csharp
    private void DrawSeries(DrawingContext context, LineSeries series, Rect plot)
    {
        var brush = Brush(series.Color);
        var dashed = new Pen(brush, 1.5, new DashStyle([4, 3], 0), PenLineCap.Round, PenLineJoin.Round);
        foreach (var estimate in series.Estimated)
        {
            DrawLine(context, [.. estimate.Select(p => new Point(X(p.X, plot), Y(p.Y, plot)))], dashed);
        }

        var pen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var points = series.Points.OrderBy(p => p.X).ToList();
        var run = new List<Point>();
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0 && points[i].X - points[i - 1].X > MaxGap)
            {
                DrawRun(context, run, pen, brush);
                run.Clear();
            }

            run.Add(new Point(X(points[i].X, plot), Y(points[i].Y, plot)));
        }

        DrawRun(context, run, pen, brush);
    }

    /// <summary>A lone point has no line to show it, so it gets a dot.</summary>
    private static void DrawRun(DrawingContext context, List<Point> run, Pen pen, IBrush brush)
    {
        if (run.Count == 1)
        {
            context.DrawEllipse(brush, null, run[0], 2.5, 2.5);
            return;
        }

        DrawLine(context, run, pen);
    }

    private static void DrawLine(DrawingContext context, List<Point> line, Pen pen)
    {
        if (line.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(line[0], isFilled: false);
            foreach (var point in line.Skip(1))
            {
                figure.LineTo(point);
            }

            figure.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }
```

Die Brücken kommen zuerst, damit die durchgezogenen Linien und Punkte darüber liegen. `TooltipAt` und `HasData` bleiben unverändert (sie lesen nur `Points`).

- [ ] **Step 2: ChartSwatch kann gestrichelt**

`ChartSwatch.cs` ersetzen:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mcacc.App.Controls;

/// <summary>The small color square of a legend entry, or a short dashed line for an estimate.</summary>
public sealed class ChartSwatch : Control
{
    private const double Side = 10;
    private const double DashWidth = 16;

    public static readonly StyledProperty<ChartColor> ColorProperty =
        AvaloniaProperty.Register<ChartSwatch, ChartColor>(nameof(Color));

    public static readonly StyledProperty<bool> DashedProperty =
        AvaloniaProperty.Register<ChartSwatch, bool>(nameof(Dashed));

    static ChartSwatch()
    {
        AffectsRender<ChartSwatch>(ColorProperty, DashedProperty);
        AffectsMeasure<ChartSwatch>(DashedProperty);
    }

    public ChartSwatch()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public ChartColor Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Drawn like the dashed estimate of a line chart instead of a square.</summary>
    public bool Dashed
    {
        get => GetValue(DashedProperty);
        set => SetValue(DashedProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var brush = ChartControl.BrushOf(this, Color);
        if (!Dashed)
        {
            context.DrawRectangle(brush, null, new Rect(0, 0, Side, Side), 2, 2);
            return;
        }

        var pen = new Pen(brush, 1.5, new DashStyle([4, 3], 0), PenLineCap.Round);
        context.DrawLine(pen, new Point(1, Side / 2), new Point(DashWidth - 1, Side / 2));
    }

    protected override Size MeasureOverride(Size availableSize) => new(Dashed ? DashWidth : Side, Side);
}
```

- [ ] **Step 3: Legenden-Template bindet `Dashed`**

`AccountInfoView.axaml`, im `DataTemplate DataType="vm:LegendItemViewModel"`:

```xml
        <controls:ChartSwatch Color="{Binding Color}" Dashed="{Binding Dashed}" VerticalAlignment="Center" />
```

- [ ] **Step 4: Leertext passt zum Autostart**

`Texts.Info.cs:42` – der Verlauf wächst jetzt auch versteckt im Infobereich:

```csharp
    public const string InfoNoHistory = "Noch kein Verlauf – er wächst, solange MCACC läuft.";
```

Kein Test erwartet den alten Text wörtlich (nur `Texts.Info.cs:42` enthält ihn).

- [ ] **Step 5: Screenshot-Test mit dem Verlauf vom 24.09.**

In `ScreenshotTests.cs` nach `Info_page_in_a_wide_window` ergänzen:

```csharp
    /// <summary>The limit history with the gaps of 2026-09-24: a lone point before a reset, and a night without MCACC.</summary>
    [AvaloniaFact]
    public async Task Info_page_with_gaps_in_the_history()
    {
        var page = InfoPage();
        var now = DateTimeOffset.Now;
        UsageHistoryPoint At(double hoursAgo, double fiveHour, double resetsInHours, double sevenDay) =>
            new(now.AddHours(-hoursAgo), fiveHour, now.AddHours(-hoursAgo + resetsInHours), sevenDay, now.AddDays(6));
        _services.Usage.History["privat"] =
        [
            At(23.4, 97, 0.53, 31),
            .. Enumerable.Range(0, 40).Select(i => At(22.3 - i * 0.08, 5 + i * 1.3, 4.9 - i * 0.08, 32 + i * 0.15)),
            .. Enumerable.Range(0, 36).Select(i => At(3 - i * 0.08, 6 + i * 1.3, 4.9 - i * 0.08, 2 + i * 0.15)),
        ];
        page.ShowLimitsViewCommand.Execute("history");

        var window = Window(page, ThemeVariant.Light, width: 1300, height: 1400);
        await page.ActivateAsync(TestContext.Current.CancellationToken);
        Save(window, "info-history-gaps");
    }
```

- [ ] **Step 6: Build, Tests, Screenshot ansehen**

Run: `dotnet build MCACC.slnx` → Expected: 0 Warnungen, 0 Fehler.
Run: `dotnet test MCACC.slnx` → Expected: alles grün (Screenshots werden ohne `MCACC_SHOTS` übersprungen).
Run (PowerShell): `$env:MCACC_SHOTS = "$env:TEMP\mcacc-shots"; dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~Info_page_with_gaps_in_the_history"; Remove-Item Env:MCACC_SHOTS`
Dann `$env:TEMP\mcacc-shots\info-history-gaps.png` ansehen (Read-Tool). Erwartung: links ein Punkt bei 97 %, gestrichelt waagrecht bis zum Reset, senkrecht auf 0, gestrichelt auf 0 bis kurz vor dem Anstieg, dann die durchgezogene Linie; eine lange gestrichelte Brücke über die Nacht; in der Legende „nicht gemessen“ mit Strichlinie.

- [ ] **Step 7: Commit**

```bash
git add src/Mcacc.App/Controls/LineChart.cs src/Mcacc.App/Controls/ChartSwatch.cs src/Mcacc.App/Views/AccountInfoView.axaml src/Mcacc.App/Localization/Texts.Info.cs tests/Mcacc.App.Tests/Views/ScreenshotTests.cs
git commit -m "feat(app): draw the estimated history dashed and name it in the legend"
```

---

### Task 4: `AppSettings.LaunchAtLogin`

**Files:**
- Modify: `src/Mcacc.Core/Model/AppSettings.cs`
- Test: `tests/Mcacc.Core.Tests/Config/McaccJsonContextTests.cs`

**Interfaces:**
- Produces: `AppSettings.LaunchAtLogin` (`bool`, letzter Parameter, Standard `true`, JSON `launchAtLogin`).

- [ ] **Step 1: Tests anpassen und ergänzen**

In `McaccJsonContextTests.cs`:

1. In `FullJson` nach `"lastTarget": "desktop"` ein Komma und die Zeile `"launchAtLogin": false` ergänzen:

```json
            "lastAccountId": "privat",
            "lastTarget": "desktop",
            "launchAtLogin": false
```

2. In `FullConfig` `LaunchTarget.Desktop));` ändern zu:

```csharp
            LastTarget: LaunchTarget.Desktop,
            LaunchAtLogin: false));
```

3. In `Omits_null_values` die erwarteten Settings:

```json
              "settings": {
                "windowsTerminalShell": "auto",
                "closeAfterLaunch": true,
                "launchAtLogin": true
              }
```

4. Neuer Test:

```csharp
    [Fact]
    public void A_config_without_launch_at_login_starts_at_login()
    {
        Deserialize("""{ "version": 1, "accounts": [], "settings": { "closeAfterLaunch": false } }""")
            .Settings.LaunchAtLogin.ShouldBeTrue();
    }
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~McaccJsonContextTests"`
Expected: Build error (`LaunchAtLogin` fehlt).

- [ ] **Step 3: Implement**

`AppSettings.cs`:

```csharp
namespace Mcacc.Core.Model;

/// <param name="ExcludedProjects">
/// The projects the info page leaves out of the Claude Code statistics, for all accounts; null when none is. Null
/// rather than empty keeps the key out of <c>config.json</c> and the default settings equal to each other.
/// </param>
/// <param name="LaunchAtLogin">MCACC starts hidden in the tray when the user logs in, so the limit history keeps growing.</param>
public sealed record AppSettings(
    string? Terminal = null,
    WindowsTerminalShell WindowsTerminalShell = WindowsTerminalShell.Auto,
    bool CloseAfterLaunch = true,
    string? LastAccountId = null,
    LaunchTarget? LastTarget = null,
    IReadOnlyList<string>? ExcludedProjects = null,
    bool LaunchAtLogin = true);
```

- [ ] **Step 4: Run all Core tests**

Run: `dotnet test tests/Mcacc.Core.Tests`
Expected: all PASS. Schlägt ein anderer Test fehl, der die JSON-Ausgabe wörtlich vergleicht, dort `"launchAtLogin": true` an der gleichen Stelle ergänzen.

- [ ] **Step 5: Commit**

```bash
git add src/Mcacc.Core/Model/AppSettings.cs tests/Mcacc.Core.Tests/Config/McaccJsonContextTests.cs
git commit -m "feat(core): add the launch at login setting, on by default"
```

---

### Task 5: `ILoginItem` für Windows und macOS

**Files:**
- Create: `src/Mcacc.Platform/ILoginItem.cs`
- Create: `src/Mcacc.Platform/Windows/WindowsLoginItem.cs`
- Create: `src/Mcacc.Platform/MacOS/MacLoginItem.cs`
- Create: `src/Mcacc.Platform/MacOS/LaunchAgentPlist.cs`
- Modify: `src/Mcacc.Platform/PlatformServices.cs`
- Test: `tests/Mcacc.Platform.Tests/Windows/WindowsLoginItemTests.cs`
- Test: `tests/Mcacc.Platform.Tests/MacOS/MacLoginItemTests.cs`

**Interfaces:**
- Produces (namespace `Mcacc.Platform`):
  - `public interface ILoginItem { void Enable(string executablePath); void Disable(); }`
  - `public sealed class NoLoginItem : ILoginItem` mit `public static readonly NoLoginItem Instance`
  - `public static class LoginItem { public const string HiddenSwitch = "--hidden"; }`
  - `PlatformServices.LoginItem` (`ILoginItem`, init, Standard `NoLoginItem.Instance`)
- Produces (namespace `Mcacc.Platform.Windows`): `WindowsLoginItem()` und `WindowsLoginItem(string keyPath, string valueName)`, `public const string RunKey`, `public static string Command(string executablePath)`.
- Produces (namespace `Mcacc.Platform.MacOS`): `MacLoginItem(string launchAgentsDir, string label = MacLoginItem.DefaultLabel)`, `public const string DefaultLabel = "de.benjaminbiber.mcacc"`; `LaunchAgentPlist.For(string label, string executablePath)`.

- [ ] **Step 1: Write the failing tests**

`tests/Mcacc.Platform.Tests/MacOS/MacLoginItemTests.cs` (läuft auf jedem OS, es schreibt nur Dateien):

```csharp
using Mcacc.Platform.MacOS;
using Mcacc.Platform.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.MacOS;

public sealed class MacLoginItemTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string AgentsDir => Path.Join(_temp.Path, "Library", "LaunchAgents");

    private string File => Path.Join(AgentsDir, "de.benjaminbiber.mcacc.plist");

    [Fact]
    public void The_plist_starts_the_binary_hidden_at_login()
    {
        LaunchAgentPlist.For("de.benjaminbiber.mcacc", "/Applications/MCACC.app/Contents/MacOS/MCACC").ShouldBe("""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>Label</key>
              <string>de.benjaminbiber.mcacc</string>
              <key>ProgramArguments</key>
              <array>
                <string>/Applications/MCACC.app/Contents/MacOS/MCACC</string>
                <string>--hidden</string>
              </array>
              <key>RunAtLoad</key>
              <true/>
              <key>LimitLoadToSessionType</key>
              <string>Aqua</string>
            </dict>
            </plist>

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void The_plist_escapes_the_path()
    {
        LaunchAgentPlist.For("x", "/Users/a&b/<MCACC>.app").ShouldContain("<string>/Users/a&amp;b/&lt;MCACC&gt;.app</string>");
    }

    [Fact]
    public void Enable_writes_the_plist_and_disable_removes_it()
    {
        var item = new MacLoginItem(AgentsDir);

        item.Enable("/Applications/MCACC.app/Contents/MacOS/MCACC");
        System.IO.File.ReadAllText(File).ShouldBe(LaunchAgentPlist.For(MacLoginItem.DefaultLabel, "/Applications/MCACC.app/Contents/MacOS/MCACC"));

        item.Disable();
        System.IO.File.Exists(File).ShouldBeFalse();
    }

    [Fact]
    public void Enable_again_follows_a_moved_app()
    {
        var item = new MacLoginItem(AgentsDir);

        item.Enable("/Applications/MCACC.app/Contents/MacOS/MCACC");
        item.Enable("/Users/ben/Apps/MCACC.app/Contents/MacOS/MCACC");

        System.IO.File.ReadAllText(File).ShouldContain("<string>/Users/ben/Apps/MCACC.app/Contents/MacOS/MCACC</string>");
    }

    [Fact]
    public void Disable_without_a_plist_does_nothing()
    {
        Should.NotThrow(() => new MacLoginItem(AgentsDir).Disable());
    }
}
```

`tests/Mcacc.Platform.Tests/Windows/WindowsLoginItemTests.cs` (Integration, echte Registry unter einem Testschlüssel):

```csharp
using System.Runtime.Versioning;
using Mcacc.Platform.Tests.TestSupport;
using Mcacc.Platform.Windows;
using Microsoft.Win32;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.Windows;

[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public sealed class WindowsLoginItemTests : IDisposable
{
    // Never the real Run key: a test must not make MCACC start at the next login.
    private readonly string _key = $@"Software\MCACC-Tests\{Guid.NewGuid():N}";

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            Registry.CurrentUser.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);
        }
    }

    private string? Value()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_key);
        return key?.GetValue("MCACC") as string;
    }

    [WindowsFact]
    public void The_command_quotes_the_path_and_starts_hidden()
    {
        WindowsLoginItem.Command(@"C:\Program Files\MCACC\MCACC.exe").ShouldBe("\"C:\\Program Files\\MCACC\\MCACC.exe\" --hidden");
    }

    [WindowsFact]
    public void The_real_key_is_the_run_key_of_the_user()
    {
        WindowsLoginItem.RunKey.ShouldBe(@"Software\Microsoft\Windows\CurrentVersion\Run");
    }

    [WindowsFact]
    public void Enable_writes_the_value_and_disable_removes_it()
    {
        var item = new WindowsLoginItem(_key, "MCACC");

        item.Enable(@"C:\Tools\MCACC.exe");
        Value().ShouldBe("\"C:\\Tools\\MCACC.exe\" --hidden");

        item.Disable();
        Value().ShouldBeNull();
    }

    [WindowsFact]
    public void Disable_without_the_key_does_nothing()
    {
        Should.NotThrow(() => new WindowsLoginItem(_key, "MCACC").Disable());
    }
}
```

Wie die anderen Windows-Klassen der Plattform (`WindowsShellIntegration` usw.) ist `WindowsLoginItem` als Ganzes `[SupportedOSPlatform("windows")]`; die Testklasse trägt dasselbe Attribut, alle Tests sind `[WindowsFact]`.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test tests/Mcacc.Platform.Tests --filter "FullyQualifiedName~LoginItem"`
Expected: Build error (Typen fehlen).

- [ ] **Step 3: Implement**

`src/Mcacc.Platform/ILoginItem.cs`:

```csharp
namespace Mcacc.Platform;

/// <summary>The entry that starts MCACC hidden in the tray when the user logs in.</summary>
public interface ILoginItem
{
    /// <summary>Creates the entry, or points it at <paramref name="executablePath"/> if it names another one.</summary>
    void Enable(string executablePath);

    /// <summary>Removes the entry; without one it does nothing.</summary>
    void Disable();
}

public static class LoginItem
{
    /// <summary>The argument of a start at login: MCACC comes up in the tray alone, without its window.</summary>
    public const string HiddenSwitch = "--hidden";
}

/// <summary>For tests and unknown platforms: there is no entry to keep.</summary>
public sealed class NoLoginItem : ILoginItem
{
    public static readonly NoLoginItem Instance = new();

    public void Enable(string executablePath)
    {
    }

    public void Disable()
    {
    }
}
```

`src/Mcacc.Platform/Windows/WindowsLoginItem.cs`:

```csharp
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Mcacc.Platform.Windows;

/// <summary>
/// A value under the user's Run key. Task Manager's own switch for it lives in <c>StartupApproved</c>, which MCACC leaves
/// alone: turned off there, the entry stays off.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsLoginItem(string keyPath, string valueName) : ILoginItem
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public WindowsLoginItem()
        : this(RunKey, "MCACC")
    {
    }

    public static string Command(string executablePath) => $"\"{executablePath}\" {LoginItem.HiddenSwitch}";

    public void Enable(string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(valueName, Command(executablePath), RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
```

`new WindowsLoginItem()` steht in `PlatformServices.Create` im Zweig `if (OperatingSystem.IsWindows())`, so bleibt die Plattform-Analyse (CA1416) still.

`src/Mcacc.Platform/MacOS/LaunchAgentPlist.cs`:

```csharp
using System.Security;

namespace Mcacc.Platform.MacOS;

/// <summary>The LaunchAgent that starts MCACC hidden at login, only in a graphical session.</summary>
public static class LaunchAgentPlist
{
    public static string For(string label, string executablePath) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key>
          <string>{SecurityElement.Escape(label)}</string>
          <key>ProgramArguments</key>
          <array>
            <string>{SecurityElement.Escape(executablePath)}</string>
            <string>{LoginItem.HiddenSwitch}</string>
          </array>
          <key>RunAtLoad</key>
          <true/>
          <key>LimitLoadToSessionType</key>
          <string>Aqua</string>
        </dict>
        </plist>

        """.ReplaceLineEndings("\n");
}
```

`src/Mcacc.Platform/MacOS/MacLoginItem.cs`:

```csharp
namespace Mcacc.Platform.MacOS;

/// <summary>
/// A plist in <c>~/Library/LaunchAgents</c>; launchd reads it at the next login, so nothing is loaded now. macOS lists it
/// under System Settings → General → Login Items, where it can be switched off as well.
/// </summary>
public sealed class MacLoginItem(string launchAgentsDir, string label = MacLoginItem.DefaultLabel) : ILoginItem
{
    public const string DefaultLabel = "de.benjaminbiber.mcacc";

    private string File => Path.Join(launchAgentsDir, label + ".plist");

    public void Enable(string executablePath)
    {
        var content = LaunchAgentPlist.For(label, executablePath);
        if (System.IO.File.Exists(File) && System.IO.File.ReadAllText(File) == content)
        {
            return;
        }

        Directory.CreateDirectory(launchAgentsDir);
        var temp = File + ".tmp";
        System.IO.File.WriteAllText(temp, content);
        System.IO.File.Move(temp, File, overwrite: true);
    }

    public void Disable()
    {
        if (System.IO.File.Exists(File))
        {
            System.IO.File.Delete(File);
        }
    }
}
```

`PlatformServices.cs`:

1. Unter der `Dock`-Eigenschaft:

```csharp
    /// <summary>The entry that starts MCACC at login; none where MCACC does not know how.</summary>
    public ILoginItem LoginItem { get; init; } = NoLoginItem.Instance;
```

2. Im Windows-Zweig von `Create` den `return new PlatformServices(…)`-Ausdruck um einen Initializer ergänzen:

```csharp
                new FileCredentialSource(),
                new UsageHttpClient())
            {
                LoginItem = new WindowsLoginItem(),
            };
```

3. Im macOS-Zweig den vorhandenen Initializer ergänzen:

```csharp
        {
            Dock = new MacDockIcon(),
            LoginItem = new MacLoginItem(Path.Join(info.HomeDir, "Library", "LaunchAgents")),
        };
```

(`PlatformInfo.HomeDir` ist das Home-Verzeichnis, wie in `MacBundleLocator`.)

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Mcacc.Platform.Tests --filter "FullyQualifiedName~LoginItem"`
Expected: all PASS (unter Windows auch die `[WindowsFact]`-Tests).
Run: `dotnet build MCACC.slnx` → 0 Warnungen.

- [ ] **Step 5: Commit**

```bash
git add src/Mcacc.Platform/ILoginItem.cs src/Mcacc.Platform/Windows/WindowsLoginItem.cs src/Mcacc.Platform/MacOS/MacLoginItem.cs src/Mcacc.Platform/MacOS/LaunchAgentPlist.cs src/Mcacc.Platform/PlatformServices.cs tests/Mcacc.Platform.Tests/Windows/WindowsLoginItemTests.cs tests/Mcacc.Platform.Tests/MacOS/MacLoginItemTests.cs
git commit -m "feat(platform): add a login item for Windows and macOS"
```

---

### Task 6: `LoginItemSync` und der Schalter in den Einstellungen

**Files:**
- Create: `src/Mcacc.App/Services/LoginItemSync.cs`
- Modify: `src/Mcacc.App/Services/AppServices.cs` (neuer Slot `LoginItems` nach `IAppLifetime Lifetime`)
- Modify: `src/Mcacc.App/CompositionRoot.cs` (anlegen, beim Start anwenden, in `AppServices` geben)
- Modify: `src/Mcacc.App/ViewModels/SettingsViewModel.cs` (`LaunchAtLogin`, `Save` gibt `bool` zurück)
- Modify: `src/Mcacc.App/Views/SettingsView.axaml:47-52` (zweiter Schalter)
- Modify: `src/Mcacc.App/Localization/Texts.cs` (`SettingsLaunchAtLogin`), `src/Mcacc.App/Localization/Texts.Tray.cs` (`SettingsLaunchAtLoginHint`)
- Modify: `tests/Mcacc.App.Tests/TestSupport/Fakes.cs` (`FakeLoginItem`), `tests/Mcacc.App.Tests/TestSupport/TestServices.cs`
- Test: `tests/Mcacc.App.Tests/Services/LoginItemSyncTests.cs`, `tests/Mcacc.App.Tests/ViewModels/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `ILoginItem` (Task 5), `AppSettings.LaunchAtLogin` (Task 4).
- Produces:
  - `public sealed class LoginItemSync(ILoginItem item, string? executablePath, bool isActive, ILog log)` mit `public void Apply(bool launchAtLogin)`, `public static LoginItemSync ForProcess(ILoginItem item, ILog log)`, `internal static bool IsActive(bool isDebugBuild, string? mcaccHome)`.
  - `AppServices.LoginItems` (`LoginItemSync`).
  - `TestServices.LoginItem` (`FakeLoginItem`), `TestServices.ExecutablePath` = `@"C:\Tools\MCACC.exe"`.

- [ ] **Step 1: Fake und TestServices**

`Fakes.cs` (am Ende, `using Mcacc.Platform;` oben ergänzen, falls nicht vorhanden):

```csharp
internal sealed class FakeLoginItem : ILoginItem
{
    /// <summary>The path of the last enable, null after a disable or before any call.</summary>
    public string? Enabled { get; private set; }

    public List<string> Calls { get; } = [];

    public Exception? ThrowOnApply { get; set; }

    public void Enable(string executablePath)
    {
        Calls.Add("enable");
        if (ThrowOnApply is { } error)
        {
            throw error;
        }

        Enabled = executablePath;
    }

    public void Disable()
    {
        Calls.Add("disable");
        if (ThrowOnApply is { } error)
        {
            throw error;
        }

        Enabled = null;
    }
}
```

`TestServices.cs`: Eigenschaften ergänzen

```csharp
    public const string ExecutablePath = @"C:\Tools\MCACC.exe";

    public FakeLoginItem LoginItem { get; } = new();
```

und im `new AppServices(…)` nach `Lifetime,` einfügen:

```csharp
            Lifetime,
            new LoginItemSync(LoginItem, ExecutablePath, isActive: true, NullLog.Instance),
```

Achtung: Eigenschaften mit Initializer (`LoginItem`) sind beim Konstruktorrumpf schon gesetzt, wie bei `Lifetime`.

- [ ] **Step 2: Write the failing tests**

`tests/Mcacc.App.Tests/Services/LoginItemSyncTests.cs`:

```csharp
using Mcacc.App.Services;
using Mcacc.App.Tests.TestSupport;
using Mcacc.Core.Logging;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Services;

public class LoginItemSyncTests
{
    private readonly FakeLoginItem _item = new();

    private LoginItemSync Sync(bool isActive = true, string? path = @"C:\Tools\MCACC.exe") =>
        new(_item, path, isActive, NullLog.Instance);

    [Fact]
    public void On_points_the_entry_at_the_running_exe()
    {
        Sync().Apply(launchAtLogin: true);

        _item.Enabled.ShouldBe(@"C:\Tools\MCACC.exe");
    }

    [Fact]
    public void Off_removes_the_entry()
    {
        var sync = Sync();
        sync.Apply(launchAtLogin: true);

        sync.Apply(launchAtLogin: false);

        _item.Enabled.ShouldBeNull();
        _item.Calls.ShouldBe(["enable", "disable"]);
    }

    [Fact]
    public void An_inactive_sync_leaves_the_entry_alone()
    {
        var sync = Sync(isActive: false);

        sync.Apply(launchAtLogin: true);
        sync.Apply(launchAtLogin: false);

        _item.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Without_a_path_nothing_is_enabled()
    {
        Sync(path: null).Apply(launchAtLogin: true);

        _item.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void A_failing_entry_does_not_throw()
    {
        _item.ThrowOnApply = new UnauthorizedAccessException("denied");

        Should.NotThrow(() => Sync().Apply(launchAtLogin: true));
    }

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(false, "", true)]
    [InlineData(true, null, false)]
    [InlineData(false, @"C:\temp\mcacc-test", false)]
    public void Only_a_release_build_with_the_real_data_folder_keeps_the_entry(bool debug, string? home, bool expected)
    {
        LoginItemSync.IsActive(debug, home).ShouldBe(expected);
    }
}
```

In `SettingsViewModelTests.cs` ergänzen (`_services` ist das `TestServices`-Feld, `Page()` legt die Seite an):

```csharp
    [Fact]
    public void Launch_at_login_starts_from_the_saved_setting()
    {
        _services.Accounts.UpdateSettings(settings => settings with { LaunchAtLogin = false });

        Page().LaunchAtLogin.ShouldBeFalse();
        _services.LoginItem.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Loading_the_page_leaves_the_entry_alone()
    {
        Page().LaunchAtLogin.ShouldBeTrue();

        _services.LoginItem.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Switching_launch_at_login_saves_it_and_updates_the_entry()
    {
        var page = Page();

        page.LaunchAtLogin = false;
        _services.Accounts.Config.Settings.LaunchAtLogin.ShouldBeFalse();
        _services.LoginItem.Calls.ShouldBe(["disable"]);

        page.LaunchAtLogin = true;
        _services.Accounts.Config.Settings.LaunchAtLogin.ShouldBeTrue();
        _services.LoginItem.Enabled.ShouldBe(TestServices.ExecutablePath);
    }
```

- [ ] **Step 3: Run to see them fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~LoginItemSyncTests|FullyQualifiedName~SettingsViewModelTests"`
Expected: Build error (`LoginItemSync`, `LaunchAtLogin` fehlen).

- [ ] **Step 4: Implement `LoginItemSync`**

`src/Mcacc.App/Services/LoginItemSync.cs`:

```csharp
using System.Security;
using Mcacc.Core.Logging;
using Mcacc.Platform;

namespace Mcacc.App.Services;

/// <summary>
/// Keeps the login entry in step with the setting, at each start and each switch. The entry always names the running
/// MCACC, so a moved app still starts. A failure is logged: the entry must never cost MCACC its start.
/// </summary>
public sealed class LoginItemSync(ILoginItem item, string? executablePath, bool isActive, ILog log)
{
#if DEBUG
    private const bool IsDebugBuild = true;
#else
    private const bool IsDebugBuild = false;
#endif

    /// <summary>For the running process: the exe it was started from, active only in a real installation.</summary>
    public static LoginItemSync ForProcess(ILoginItem item, ILog log) => new(
        item,
        Environment.ProcessPath,
        IsActive(IsDebugBuild, Environment.GetEnvironmentVariable("MCACC_HOME")),
        log);

    /// <summary>
    /// A debug build would make the login start the exe in <c>bin/Debug</c>, and a run under its own data folder is a
    /// test that must not touch the real entry.
    /// </summary>
    internal static bool IsActive(bool isDebugBuild, string? mcaccHome) =>
        !isDebugBuild && string.IsNullOrWhiteSpace(mcaccHome);

    public void Apply(bool launchAtLogin)
    {
        if (!isActive)
        {
            return;
        }

        try
        {
            if (!launchAtLogin)
            {
                item.Disable();
            }
            else if (executablePath is { } path)
            {
                item.Enable(path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            log.Warn($"Updating the login entry (launch at login: {launchAtLogin}) failed.", e);
        }
    }
}
```

- [ ] **Step 5: Verdrahten**

`AppServices.cs`: nach `IAppLifetime Lifetime,` einfügen:

```csharp
    IAppLifetime Lifetime,
    LoginItemSync LoginItems,
```

`CompositionRoot.cs`: vor `var services = new AppServices(` einfügen

```csharp
        var loginItems = LoginItemSync.ForProcess(platform.LoginItem, log);
        loginItems.Apply(accounts.Config.Settings.LaunchAtLogin);
```

und im `new AppServices(…)` nach `lifetime,` den Wert `loginItems,` übergeben.

- [ ] **Step 6: Einstellungen**

`Texts.cs` unter `SettingsCloseAfterLaunch`:

```csharp
    public const string SettingsLaunchAtLogin = "Bei der Anmeldung starten";
```

`Texts.Tray.cs` unter `SettingsTrayHint`:

```csharp
    public static string SettingsLaunchAtLoginHint(OsKind os) => os == OsKind.MacOS
        ? "MCACC startet dann versteckt in der Menüleiste und zeichnet die Limits auf."
        : "MCACC startet dann versteckt im Infobereich und zeichnet die Limits auf.";
```

`SettingsViewModel.cs`:

1. Im Konstruktor nach `CloseAfterLaunch = settings.CloseAfterLaunch;`: `LaunchAtLogin = settings.LaunchAtLogin;`
2. Unter `TrayHint`:

```csharp
    public string LaunchAtLoginText => Texts.SettingsLaunchAtLogin;

    public string LaunchAtLoginHint => Texts.SettingsLaunchAtLoginHint(_services.Platform.Os);
```

3. Unter der Eigenschaft `CloseAfterLaunch`:

```csharp
    [ObservableProperty]
    public partial bool LaunchAtLogin { get; set; }
```

4. Unter `OnCloseAfterLaunchChanged`:

```csharp
    partial void OnLaunchAtLoginChanged(bool value)
    {
        if (Save(settings => settings with { LaunchAtLogin = value }))
        {
            _services.LoginItems.Apply(value);
        }
    }
```

5. `Save` gibt zurück, ob gespeichert wurde:

```csharp
    /// <returns>False while loading and when the file could not be written.</returns>
    private bool Save(Func<AppSettings, AppSettings> change)
    {
        if (_isLoading)
        {
            return false;
        }

        try
        {
            _services.Accounts.UpdateSettings(change);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _services.Log.Error("Saving the settings failed.", e);
            _services.Shell.ShowError(e.Message);
            return false;
        }
    }
```

`SettingsView.axaml`: nach dem `StackPanel` mit `CloseAfterLaunch` (Zeilen 47–52) einfügen:

```xml
      <StackPanel Spacing="2">
        <ToggleSwitch IsChecked="{Binding LaunchAtLogin}"
                      OnContent="{Binding LaunchAtLoginText}"
                      OffContent="{Binding LaunchAtLoginText}" />
        <TextBlock Classes="caption muted" Text="{Binding LaunchAtLoginHint}" TextWrapping="Wrap" />
      </StackPanel>
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test MCACC.slnx`
Expected: all PASS (auch `TextsTests`, `PageRenderTests`). `dotnet build MCACC.slnx` → 0 Warnungen.

- [ ] **Step 8: Commit**

```bash
git add src/Mcacc.App/Services/LoginItemSync.cs src/Mcacc.App/Services/AppServices.cs src/Mcacc.App/CompositionRoot.cs src/Mcacc.App/ViewModels/SettingsViewModel.cs src/Mcacc.App/Views/SettingsView.axaml src/Mcacc.App/Localization/Texts.cs src/Mcacc.App/Localization/Texts.Tray.cs tests/Mcacc.App.Tests/TestSupport/Fakes.cs tests/Mcacc.App.Tests/TestSupport/TestServices.cs tests/Mcacc.App.Tests/Services/LoginItemSyncTests.cs tests/Mcacc.App.Tests/ViewModels/SettingsViewModelTests.cs
git commit -m "feat(app): keep the login entry in step with a launch at login switch"
```

---

### Task 7: Start mit `--hidden`

**Files:**
- Create: `src/Mcacc.App/StartupArgs.cs`
- Modify: `src/Mcacc.App/Program.cs:22-27`
- Modify: `src/Mcacc.App/App.axaml.cs:30-40` (`Start`)
- Test: `tests/Mcacc.App.Tests/StartupArgsTests.cs`

**Interfaces:**
- Consumes: `LoginItem.HiddenSwitch` (Task 5), `IDockIcon.SetVisible`.
- Produces: `internal static class StartupArgs { public static bool IsHidden(IEnumerable<string>? args); public static bool ShouldSignalExisting(IEnumerable<string>? args); }`

- [ ] **Step 1: Write the failing tests**

`tests/Mcacc.App.Tests/StartupArgsTests.cs`:

```csharp
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests;

public class StartupArgsTests
{
    [Fact]
    public void A_plain_start_shows_the_window()
    {
        StartupArgs.IsHidden([]).ShouldBeFalse();
        StartupArgs.IsHidden(null).ShouldBeFalse();
    }

    [Theory]
    [InlineData("--hidden")]
    [InlineData("--HIDDEN")]
    public void A_start_at_login_stays_in_the_tray(string arg)
    {
        StartupArgs.IsHidden(["--foo", arg]).ShouldBeTrue();
    }

    [Fact]
    public void A_second_plain_start_brings_the_running_window_up()
    {
        StartupArgs.ShouldSignalExisting([]).ShouldBeTrue();
    }

    [Fact]
    public void A_second_start_at_login_leaves_the_running_mcacc_alone()
    {
        StartupArgs.ShouldSignalExisting(["--hidden"]).ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~StartupArgsTests"`
Expected: Build error (`StartupArgs` fehlt).

- [ ] **Step 3: Implement**

`src/Mcacc.App/StartupArgs.cs`:

```csharp
using Mcacc.Platform;

namespace Mcacc.App;

/// <summary>What the command line of a start asks for.</summary>
internal static class StartupArgs
{
    /// <summary>A start at login: MCACC comes up in the tray alone.</summary>
    public static bool IsHidden(IEnumerable<string>? args) =>
        args?.Contains(LoginItem.HiddenSwitch, StringComparer.OrdinalIgnoreCase) == true;

    /// <summary>
    /// A second start brings the running window up, but not a start at login: it finds MCACC already running and has
    /// nothing to show.
    /// </summary>
    public static bool ShouldSignalExisting(IEnumerable<string>? args) => !IsHidden(args);
}
```

`Program.cs` – den Zweig für eine zweite Instanz ersetzen:

```csharp
        using var single = SingleInstance.Acquire();
        if (!single.IsOnlyInstance)
        {
            if (StartupArgs.ShouldSignalExisting(args))
            {
                SingleInstance.SignalExisting();
            }

            return 0;
        }
```

`App.axaml.cs` – in `Start` die Zeilen

```csharp
        var window = new MainWindow { DataContext = shell, Lifetime = lifetime };
        lifetime.Window = window;
        desktop.MainWindow = window;
```

ersetzen durch:

```csharp
        var window = new MainWindow { DataContext = shell, Lifetime = lifetime };
        lifetime.Window = window;
        if (StartupArgs.IsHidden(desktop.Args))
        {
            // Without a main window the lifetime shows none; the tray and the poller run all the same.
            platform.Dock.SetVisible(false);
        }
        else
        {
            desktop.MainWindow = window;
        }
```

Und die Doku von `Start` ergänzen:

```csharp
    /// <summary>
    /// MCACC lives in the tray: the window comes and goes, and only the tray's quit or Cmd+Q ends the process. A start
    /// at login (<see cref="StartupArgs.IsHidden"/>) leaves the window hidden until the tray or a second start asks for it.
    /// </summary>
```

Prüfen, dass nichts anderes `desktop.MainWindow` voraussetzt: `grep -rn "MainWindow" src/Mcacc.App --include=*.cs | grep -v "MainWindowViewModel\|class MainWindow\|new MainWindow"`. Erwartung: nur die Zuweisung in `App.axaml.cs`. `DesktopAppLifetime.ShowWindow` nutzt `Window`, nicht `MainWindow`.

- [ ] **Step 4: Run the tests and build**

Run: `dotnet test tests/Mcacc.App.Tests` → all PASS.
Run: `dotnet build MCACC.slnx` → 0 Warnungen.

- [ ] **Step 5: Commit**

```bash
git add src/Mcacc.App/StartupArgs.cs src/Mcacc.App/Program.cs src/Mcacc.App/App.axaml.cs tests/Mcacc.App.Tests/StartupArgsTests.cs
git commit -m "feat(app): start hidden in the tray with --hidden"
```

---

### Task 8: Poller überlebt einen werfenden Tick

**Files:**
- Modify: `src/Mcacc.App/Services/UsagePoller.cs` (Konstruktor `interval`, `RunAsync`)
- Test: `tests/Mcacc.App.Tests/Services/UsagePollerTests.cs`

**Interfaces:**
- Produces: `UsagePoller(IAccountService accounts, IUsageProvider usage, ILog log, IReadOnlyList<TimeSpan>? recheckAfter = null, TimeSpan? interval = null)`; `UsagePoller.Interval` bleibt der Standard (5 min).

- [ ] **Step 1: Write the failing test**

In `UsagePollerTests.cs`:

```csharp
    [Fact]
    public async Task A_throwing_handler_does_not_end_the_polling()
    {
        using var poller = new UsagePoller(_accounts, _usage, NullLog.Instance, interval: TimeSpan.FromMilliseconds(10));
        var calls = 0;
        var again = new TaskCompletionSource();
        poller.Updated += (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("A page failed to show the limits.");
            }

            again.TrySetResult();
        };

        poller.Start();

        await again.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }
```

- [ ] **Step 2: Run to see it fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~UsagePollerTests"`
Expected: Build error (`interval` fehlt). Nach Step 3a (nur Parameter) ohne 3b: FAIL mit `TimeoutException` – die Schleife endet nach dem ersten Wurf.

- [ ] **Step 3: Implement**

a) Konstruktor und Feld:

```csharp
public sealed class UsagePoller(
    IAccountService accounts,
    IUsageProvider usage,
    ILog log,
    IReadOnlyList<TimeSpan>? recheckAfter = null,
    TimeSpan? interval = null) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
```

und unter `_recheckAfter`:

```csharp
    private readonly TimeSpan _interval = interval ?? Interval;
```

b) `RunAsync`:

```csharp
    /// <summary>
    /// MCACC lives on in the tray for days, so a tick that throws, e.g. in an <see cref="Updated"/> handler, is logged
    /// and the next one comes all the same; only the own cancellation ends the loop.
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(true))
            {
                try
                {
                    await TickAsync(ct).ConfigureAwait(true);
                }
                catch (Exception e) when (!ct.IsCancellationRequested)
                {
                    log.Warn("A poll of the limits failed.", e);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~UsagePollerTests"` → all PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Mcacc.App/Services/UsagePoller.cs tests/Mcacc.App.Tests/Services/UsagePollerTests.cs
git commit -m "fix(app): keep polling after a tick that throws"
```

---

### Task 9: README, Gesamtprüfung, Tickets

**Files:**
- Modify: `README.md` (Info-Seite: Verlauf; Infobereich/Menüleiste: Autostart)

- [ ] **Step 1: README ergänzen**

`grep -n "Verlauf\|Infobereich\|Menüleiste" README.md` – im Abschnitt über den Limit-Verlauf einen Satz ergänzen:

> Lücken, in denen MCACC nicht lief, sind gestrichelt geschätzt: Fällt ein Reset hinein, hält die Linie bis zum Reset, fällt auf 0 und steigt ab dem Beginn des nächsten Fensters zum nächsten Messpunkt.

Im Abschnitt über den Infobereich bzw. die Menüleiste:

> **Bei der Anmeldung starten** (Einstellungen, standardmäßig an): MCACC startet versteckt im Infobereich bzw. in der Menüleiste und zeichnet die Limits auf. Unter Windows steht dafür der Wert `MCACC` unter `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, unter macOS `~/Library/LaunchAgents/de.benjaminbiber.mcacc.plist`. Debug-Builds und Läufe mit `MCACC_HOME` legen keinen Eintrag an.

- [ ] **Step 2: Gesamtprüfung**

Run: `dotnet build MCACC.slnx -c Release` → 0 Warnungen, 0 Fehler.
Run: `dotnet test MCACC.slnx` → alles grün; Anzahl der Tests notieren.
Run: `git status` → sauber bis auf README.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: describe the estimated history and the launch at login"
```

- [ ] **Step 4: Tickets**

```powershell
$K = "C:\Kanban\kanban.js"
node $K comment MCA-0013 "Umgesetzt (Tasks 1-3). Nachweis: <Testzahl> Tests grün, Screenshot info-history-gaps.png geprüft." --board mcacc --author claude
node $K comment MCA-0014 "Umgesetzt (Tasks 4-8). Zu prüfen: Release-Build starten, Schalter an → Run-Wert/plist vorhanden; ab- und anmelden → MCACC nur im Infobereich bzw. in der Menüleiste. Nicht geprüft: echter Login-Start, macOS." --board mcacc --author claude
node $K set MCA-0014 --status blockiert --blocked-reason "Human Interaction" --board mcacc --by claude
```

MCA-0013 geht nach `erledigt`, wenn Tests und Screenshot stimmen: `node $K move MCA-0013 erledigt --board mcacc --by claude`.
