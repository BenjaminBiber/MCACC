# MCACC – Lücken im Limit-Verlauf schätzen, Autostart bei der Anmeldung

Tickets: MCA-0013 (Schätzung), MCA-0014 (Autostart) · Ergänzt den Verlauf aus MCA-0009 und das Tray aus MCA-0010.

## Context

Der Limit-Verlauf auf der Info-Seite hat Lücken. Punkte entstehen nur, solange MCACC läuft (`UsagePoller`, alle 5 Min.), und die Linie bricht ab, sobald zwei Punkte weiter als `HistoryGap` auseinanderliegen (0,5 h in der 24-h-Ansicht, 2 h in der Wochenansicht). Am 24.09. fehlten so 12:08–13:15 (ein einzelner Punkt bei 97 %, dazwischen der 5-h-Reset) und die Nacht 16:34–08:34.

Seit MCA-0010 läuft MCACC im Infobereich weiter, wenn das Fenster geschlossen wird. Die Aufzeichnung endet also nur noch mit „MCACC beenden“ oder mit einer Anmeldung, nach der niemand MCACC startet. Schlafender oder ausgeschalteter Rechner lässt sich ohnehin nicht aufzeichnen.

## Entscheidungen (mit Benjamin geklärt, 2026-09-25)

| Thema | Entscheidung |
|---|---|
| Hintergrund | Autostart bei der Anmeldung, versteckt im Infobereich bzw. in der Menüleiste. Kein eigener Recorder-Prozess, kein Windows-Dienst (der liefe nicht als der Benutzer und käme nicht an dessen Tokens). |
| Schalter | „Bei der Anmeldung starten“ in den Einstellungen, standardmäßig an. |
| Schätzung | Treppe + Brücken: Jede Lücke wird gestrichelt gefüllt. Fällt ein Reset hinein, hält die Linie bis zum Reset, fällt auf 0, bleibt bis zum Beginn des neuen Fensters auf 0 und steigt zum nächsten Messpunkt. Ohne Reset verbindet eine gerade gestrichelte Linie. |
| Nicht im Umfang | Aufzeichnen nach „MCACC beenden“; Schätzung über den letzten Punkt hinaus bis „jetzt“. |

## Teil 1 – Schätzung (MCA-0013)

### Core: `UsageGapEstimate`

Eine reine Funktion in `Mcacc.Core.Usage`:

```csharp
public readonly record struct UsageValue(DateTimeOffset At, double Value);

public static IReadOnlyList<UsageValue> Bridge(
    UsageValue a, DateTimeOffset? aResetsAt,
    UsageValue b, DateTimeOffset? bResetsAt,
    TimeSpan window);

/// <summary>The part of a line from start on; the crossing is interpolated.</summary>
public static IReadOnlyList<UsageValue> From(IReadOnlyList<UsageValue> line, DateTimeOffset start);
```

`window` ist die Fensterlänge der Reihe: `UsageGapEstimate.FiveHour` (5 h) bzw. `UsageGapEstimate.SevenDay` (7 Tage). Innerhalb eines Fensters steigt die Auslastung nur; das neue Fenster von b begann bei `bStart = bResetsAt − window`.

| Fall | Eckpunkte |
|---|---|
| `aResetsAt` liegt nach `bAt` (kein Reset in der Lücke) | `(aAt, a)`, `(bAt, b)` |
| `aResetsAt` liegt nicht nach `bAt` | `(aAt, a)`, `(R, a)`, `(R, 0)`, `(S, 0)`, `(bAt, b)` mit `R = aResetsAt` begrenzt auf `[aAt, bAt]`, `S = bStart` begrenzt auf `[R, bAt]`; ohne `bResetsAt` ist `S = R` |
| `aResetsAt` fehlt (kein aktives Fenster) | `(aAt, a)`, `(S, a)`, `(bAt, b)` mit `S = bStart` begrenzt auf `[aAt, bAt]`; ohne `bResetsAt` gerade Linie |

Eckpunkte mit gleichem Zeitpunkt und Wert wie ihr Vorgänger fallen weg, damit keine Nulllängen-Stücke entstehen.

### ViewModel

- `LoadHistory` liest eine Spanne weiter zurück (`start − span`), damit die Brücke auch vom linken Rand her kommt. Punkte vor `start` werden nicht als Messpunkte gezeichnet, nur als Anfang einer Brücke; `UsageGapEstimate.From` schneidet die Brücke am linken Rand ab. `IUsageProvider` bleibt unverändert.
- Für jedes Paar aufeinanderfolgender Punkte einer Reihe, das weiter als `HistoryGap` auseinanderliegt, entsteht eine Brücke. Enger liegende Paare verbindet weiter die durchgezogene Linie.
- `LineSeries` bekommt `IReadOnlyList<IReadOnlyList<ChartPoint>> Estimated` (Standard leer).

### LineChart

- `Estimated` wird in der Farbe der Reihe gezeichnet, 1,5 px, `DashStyle([4, 3])`, runde Enden.
- Geschätzte Stücke haben keine Tooltips. Der Tooltip-Treffer bleibt bei den Messpunkten.
- `HasData` bleibt an den Messpunkten.

### Legende

Neben „5h“ und „Woche“ steht „╌ nicht gemessen“, sobald eine der Reihen eine Brücke hat.

## Teil 2 – Autostart (MCA-0014)

### Einstellung

- `AppSettings.LaunchAtLogin` (`bool`, JSON `launchAtLogin`, Standard `true`). Eine Konfiguration ohne den Schlüssel lädt als `true`.
- Einstellungen: Schalter „Bei der Anmeldung starten“ unter „Nach dem Start ausblenden“. Ein Klick speichert sofort und gleicht den Eintrag ab.

### Platform: `ILoginItem`

```csharp
public interface ILoginItem
{
    void Enable(string executablePath);
    void Disable();
}
```

- **Windows (`WindowsLoginItem`):** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, Wert `MCACC` = `"<exe>" --hidden`. `Disable` löscht den Wert. Den Zustand in `StartupApproved` (Task-Manager → Autostart) fasst MCACC nicht an.
- **macOS (`MacLoginItem`):** `~/Library/LaunchAgents/de.benjaminbiber.mcacc.plist` mit `Label`, `ProgramArguments = [<Binary im Bundle>, --hidden]`, `RunAtLoad = true`, `LimitLoadToSessionType = Aqua`. Der Inhalt kommt aus einer reinen Funktion `LaunchAgentPlist.For(label, executable)`. `Disable` löscht die Datei. `launchctl` wird nicht aufgerufen; die Datei wirkt ab der nächsten Anmeldung.
- Fehler beim Schreiben werden geloggt und stoppen den Start nicht.
- `PlatformServices.LoginItem`, Standard `NoLoginItem` (tut nichts).

### Abgleich: `LoginItemSync` (App)

- Läuft beim Start und bei jedem Umschalten: `LaunchAtLogin` an → `Enable(Environment.ProcessPath)`, aus → `Disable()`.
- Übersprungen in Debug-Builds und wenn `MCACC_HOME` gesetzt ist. Sonst würde die Anmeldung die EXE aus `bin/Debug` starten, und Testläufe schrieben in die echte Registry.
- Ohne `ProcessPath` (unbekannter Pfad) passiert nichts.

### Start mit `--hidden`

- `Program` reicht `--hidden` an `App` weiter. MCACC startet dann ohne Fenster, nur im Infobereich; auf macOS erscheint kein Dock-Icon.
- Account-Auswahl, erstes Laden der Limits und Poller laufen wie bei einem normalen Start.
- Läuft MCACC schon, holt ein zweiter Start mit `--hidden` das Fenster **nicht** nach vorne.

### Poller robust für den Dauerbetrieb

Aus Notiz n4 (MCA-0009), Punkt 2: `UsagePoller.RunAsync` endet heute still, wenn ein `Updated`-Handler wirft. Im Dauerbetrieb hieße das: MCACC läuft, zeichnet aber nicht mehr auf. Jeder Tick bekommt ein eigenes `try/catch` mit Log; nur das eigene Abbrechen beendet die Schleife.

## Tests

- **Core:**
  - `UsageGapEstimate`: der echte Fall vom 24.09. (12:08 97 %, Reset 12:40, neues Fenster 13:10, 13:15 5 %), kein Reset in der Lücke, `aResetsAt` fehlt, `bResetsAt` fehlt, Fensterbeginn vor dem Reset und nach `bAt` wird begrenzt, Wochenfenster.
  - `UsageGapEstimate.From`: Schnitt mitten in einem Stück, an einer senkrechten Kante, alles vor bzw. nach dem Start.
  - JSON: `launchAtLogin` im Round-Trip; ohne Schlüssel `true`.
- **App:**
  - `AccountInfoViewModel`: Brücken nur über `HistoryGap`, Brücke vom linken Rand, Legende nur mit Brücke.
  - `LoginItemSync` mit einem Fake-`ILoginItem`: an, aus, Umschalten, übersprungen bei `MCACC_HOME`.
  - `LaunchAgentPlist.For`: Inhalt und XML-Escaping des Pfads.
  - `StartupArgs.IsHidden` und der zweite Start mit `--hidden`. Dass kein Fenster erscheint, prüft ein Start des Release-Builds von Hand, weil `App.Start` die echte Plattform anlegt.
  - Screenshot (`MCACC_SHOTS`): Info-Seite mit dem Verlauf vom 24.09.
  - `UsagePoller`: ein werfender Handler beendet die Schleife nicht.
- **Von Hand (Benjamin):** Autostart nach Ab- und Anmelden unter Windows und macOS.
