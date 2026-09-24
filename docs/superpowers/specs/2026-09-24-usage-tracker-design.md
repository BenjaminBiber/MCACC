# MCACC – Usage Tracker (Info-Page pro Account)

Ticket: MCA-0009 · Ergänzt `2026-09-23-mcacc-design.md`.

## Context

Die Kachel zeigt pro Account nur zwei Balken, 5h und Woche, jeweils mit Reset-Zeit. Gespeichert wird nur der letzte Stand (`usage-cache.json`). Einen Verlauf gibt es nicht, und auch keine Anzeige der Profil-Daten.

Verfügbar ist aber mehr:
- **`/api/oauth/profile`** liefert Name, Plan-Flags, Erstelldatum, Organisation, Billing-Typ und Rate-Limit-Tier. Der Endpunkt wird für die Besitzerprüfung ohnehin schon aufgerufen.
- **`/api/oauth/usage`** liefert neben `five_hour` und `seven_day` weitere Fenster (`seven_day_opus` u. a.).
- **Credentials** liefern `subscriptionType`, `scopes` und `expiresAt`.
- **`.claude.json`** liefert `organizationRole`, `numStartups`, `installMethod` und `projects`.
- **Die Session-Logs von Claude Code** (`<claudeDir>/projects/**/*.jsonl`) enthalten pro Antwort Tokens, Modell, Zeitstempel, Projekt (`cwd`) und Session. Weil jeder Account ein eigenes Claude-Verzeichnis hat, lassen sich die Logs sauber pro Account trennen.

Ziel ist eine **Info-Page pro Account**, erreichbar über das ⋮-Menü der Kachel. Sie zeigt alle auslesbaren Daten und dazu Diagramme:
- den Verlauf der Limits
- die Token-Nutzung aus den Logs, mit geschätzten API-Kosten

Die Kachel bleibt unverändert.

## Entscheidungen (mit Benjamin geklärt, 2026-09-24)

| Thema | Entscheidung |
|---|---|
| Datenquelle der Diagramme | beides: lokale Claude-Code-Logs und neuer Limit-Verlauf |
| Ort | nur auf der Info-Page, keine Änderung an der Kachel |
| Erfassung Limit-Verlauf | jeder erfolgreiche Abruf, dazu ein Timer alle 5 min, solange MCACC offen ist |
| Kosten | Schätzung als API-Äquivalent (USD), immer mit „≈“ gekennzeichnet |
| Chart-Technik | eigene Controls mit `Render(DrawingContext)`, keine Chart-Bibliothek |

## Info-Page

Einstieg über den neuen Menüpunkt „Info & Statistik“ im ⋮-Menü der Kachel. Der Kopf folgt dem Muster der Zielauswahl: Zurück-Pfeil, Titel und Account-Chip. Esc, Backspace und Alt+← führen zurück. Die Seite scrollt.

```
← Zurück   Info & Statistik              (●) Privat · max@example.com
───────────────────────────────────────────────────────────────────
KONTO                                                     [Karte]
 E-Mail · Name · Plan (Max/Pro) · Organisation · Rolle · Billing
 Rate-Limit-Tier · Konto erstellt · Token läuft ab · Scopes
 Claude Code: Starts, Installationsart, Projekte · Desktop: läuft seit …

LIMITS                                    Stand 09:41  [↻]
 ( 42 % )     ( 71 % )     ( 12 % )      ← Ringe: 5h · Woche · weitere Fenster
 Reset 14:30  Reset Do 09:00  …
 Verlauf  [24 h | 7 Tage]
 ┌──────────────────────────────────────────┐  Linien: 5h / Woche
 │   ╱╲      ╱‾‾╲                           │  90-%-Linie gestrichelt
 └──────────────────────────────────────────┘

CLAUDE CODE NUTZUNG                   [7 | 30 | 90 Tage]
 ┌ Tokens ┐ ┌ ≈ Kosten ┐ ┌ Sessions ┐ ┌ Nachrichten ┐ ┌ Aktive Tage ┐
 Tokens pro Tag – gestapelt nach Modell (Balken)
 Token-Arten (Ring): Input · Output · Cache-Write · Cache-Read
 Top-Projekte (horizontale Balken, Tokens + ≈ Kosten)
 Modelle (Tabelle: Tokens, Anteil, ≈ Kosten)
```

- **Fehlende Felder:** Ein Feld, das keine Quelle liefert, wird ausgeblendet und nicht mit „—“ gefüllt. Weitere Limit-Fenster erscheinen nur, wenn die API sie liefert.
- **Warnfarbe:** Ab 90 % wird `Mcacc.Danger` verwendet, wie bei der `UsageBar`.
- **Kosten:** Sie tragen immer „≈“ und den Hinweis „API-Äquivalent, Schätzung“. Für ein Modell ohne Preis gibt es keinen Betrag. Die Summe nennt dann, wie viele Tokens nicht bewertet sind.
- **Tooltips:** Hover über Balken, Punkt oder Segment zeigt den genauen Wert.
- **Desktop:** Es gibt nur „läuft seit“ aus `DesktopInstance.StartTime`. Claude Desktop legt keine auswertbaren Logs ab.
- **Leerzustände:**
  - Ist kein Claude-Code-Login vorhanden, erscheint der Hinweis „nicht angemeldet“.
  - Fehlt der `projects`-Ordner, erscheint „noch keine Claude-Code-Sitzungen“.
  - Scheitert ein API-Abruf, bleibt der letzte Stand sichtbar, zusammen mit dem vorhandenen Hinweistext für den `UsageState`.

## Datenquellen und Speicherung

### Claude-Code-Logs

- **Dateien:** `EffectiveClaudeDir/projects/**/*.jsonl`, rekursiv. Das schließt `<session>/subagents/agent-*.jsonl` ein.
- **Auswahl:** Nur Zeilen mit `type == "assistant"` und `message.usage` zählen. Ein Vorfilter auf `"usage"` spart das Parsen der übrigen Zeilen.
- **Felder:**

  | Zweck | Felder |
  |---|---|
  | Zuordnung | `message.id`, `requestId`, `message.model`, `timestamp`, `cwd`, `sessionId` |
  | Input | `usage.input_tokens` |
  | Output | `usage.output_tokens` |
  | Cache-Read | `usage.cache_read_input_tokens` |
  | Cache-Write | `usage.cache_creation.ephemeral_5m_input_tokens` / `ephemeral_1h_input_tokens`; fehlen diese, zählt `cache_creation_input_tokens` als 5m |

- **Dedup:** Eine Antwort steht pro Content-Block mehrfach im Log, jeweils mit identischer `usage`. Gezählt wird eindeutig nach dem Hash von `message.id|requestId`, und zwar über alle Dateien des Accounts.
- **Robustheit:** Kaputte oder unvollständige Zeilen, etwa die letzte Zeile während eines Schreibvorgangs, werden übersprungen und gezählt. Beim nächsten Scan wird eine unvollständige letzte Zeile erneut gelesen: Der gespeicherte Offset steht immer hinter dem letzten `\n`.
- **Größe:** Auf dem Entwicklungsrechner liegen 693 Dateien mit 725 MB. Der erste Scan läuft deshalb im Hintergrund mit Fortschritt. Jeder weitere Scan liest nur, was neu hinzugekommen ist.

### Stats-Index `~/.mcacc/stats/<accountId>.json`

- **Inhalt pro Datei:** relativer Pfad, `Length`, `LastWriteUtc`, gelesener Offset, Buckets, ID-Hashes.
- **Abgleich beim Scan:**
  - Datei unverändert: wird übersprungen.
  - Datei gewachsen, `LastWriteUtc` neuer: wird ab dem Offset weitergelesen.
  - Datei kleiner geworden oder Offset ungültig: wird komplett neu gelesen.
  - Datei verschwunden: ihr Eintrag wird entfernt.
- **Bucket:** Schlüssel ist (UTC-Stunde, Modell, Projekt, Session). Werte sind Input, Output, CacheWrite5m, CacheWrite1h, CacheRead und Messages. Stunden statt Tagen, damit die Tage in lokaler Zeit korrekt geschnitten werden.
- **Speichern:** atomar wie `usage-cache.json`. Eine unlesbare Datei gilt als leer und wird beim nächsten Scan neu aufgebaut.

### Limit-Verlauf `~/.mcacc/usage-history/<accountId>.jsonl`

- **Zeilen:** eine pro erfolgreichem Abruf, als `UsageHistoryPoint(At, FiveHour, FiveHourResetsAt, SevenDay, SevenDayResetsAt)`. Die Prozentwerte sind nullable.
- **Schreiben:** Die Datei wird nur angehängt, und zwar aus `UsageService.Store`. Das ist die einzige Stelle, an der ein frischer Ok-Wert entsteht.
- **Aufräumen:** Höchstens einmal täglich werden beim Anhängen Punkte älter als 90 Tage entfernt.
- **Robustheit:** Kaputte Zeilen werden beim Lesen übersprungen.

### Profil-Cache `~/.mcacc/profile-cache.json`

- `Dictionary<accountId, AccountProfile>`, ohne Tokens.
- Wird geschrieben, sobald `/profile` erfolgreich war, damit die Info-Page auch offline die Kontodaten zeigt.

### Löschen

Wird ein Account entfernt oder sein Usage-Stand vergessen (`UsageService.Forget`), verschwinden auch Verlauf, Stats-Index und Profil-Eintrag.

### Kostenschätzung

- **Preistabelle:** `ModelPricing` ist fest eingebaut. Sie ordnet die Modell-ID nach Familien-Präfix zu und kennt fünf Sätze in USD pro Million Tokens: Input, Output, Cache-Write 5m, Cache-Write 1h, Cache-Read.
- **Quelle der Preise:** die aktuelle Anthropic-Preisliste zum Zeitpunkt der Umsetzung.
- **Unbekannte Modelle:** Sie haben keinen Preis und gehen nicht mit 0 in die Summe ein.

## Architektur

```
src/Mcacc.Core/
  Stats/        ClaudeLogScanner, LogLineParser, TokenBucket, UsageStatsIndex (+ Store), AccountStatsQuery → AccountStats,
                ModelPricing, CostEstimator, ClaudeStatsProvider
  Usage/        ProfileParser → AccountProfile (ersetzt TokenOwnerParser; TokenOwner wird daraus abgeleitet),
                UsageResponseParser (+ Extra-Fenster), UsageHistoryStore, ProfileCache
  ClaudeCode/   CredentialsParser (+ subscriptionType, scopes), ClaudeJsonLoginReader (+ Rolle, Starts, Installationsart, Projektzahl)
  Abstractions/ IClaudeStatsProvider; IUsageProvider + GetHistory, GetProfile
src/Mcacc.App/
  Services/     UsagePoller (PeriodicTimer 5 min, Event Updated)
  ViewModels/   AccountInfoViewModel (+ Unter-VMs für Kennzahlen, Ringe, Chart-Serien)
  Views/        AccountInfoView
  Controls/     ChartScale, LineChart, StackedBarChart, DonutChart, BarList
  Theme/        Tokens Mcacc.Series1..6 (Hell/Dunkel)
```

**Datenfluss beim Öffnen der Info-Page:**
1. Profil, Limits und Verlauf aus den Caches anzeigen, ohne IO-Wartezeit.
2. `RefreshAsync` für den Account aufrufen. Die 60-s-Sperre des `UsageService` gilt weiterhin.
3. Parallel dazu `IClaudeStatsProvider.LoadAsync` mit Fortschritt starten. Danach berechnet `AccountStatsQuery` die Werte für den gewählten Zeitraum, und zwar in der lokalen Zeitzone.
4. `UsagePoller.Updated` löst ein Neuladen der Limits aus. Beim Verlassen der Seite wird das Event abgemeldet.

**Poller:** Er startet mit dem Fenster und endet mit ihm. Pro Tick ruft er `RefreshAsync` für alle Accounts parallel auf. Der Picker aktualisiert seine Kacheln bei `Updated` ebenfalls.

**Charts:**
- Jedes Chart ist ein `Control` mit Style-Properties (`AffectsRender`) für die Serien, die Brushes und den Leerzustand.
- Achsen-Maxima, Ticks und Koordinaten berechnet die reine Klasse `ChartScale`.
- Hover: Ein Hit-Test in `PointerMoved` setzt `ToolTip.Tip`.
- Alle Farben kommen aus den Theme-Tokens.

## Tests

- **Core:**
  - Scanner mit JSONL-Fixtures: Dedup über Content-Blöcke und Dateien, Subagent-Ordner, kaputte und halbe Zeilen, Fallback auf `cache_creation_input_tokens`.
  - Index: Weiterlesen einer gewachsenen Datei, gekürzte und gelöschte Dateien.
  - `AccountStatsQuery`: Tagesgrenzen in einer Nicht-UTC-Zeitzone, Top-N, aktive Tage.
  - `CostEstimator`: bekanntes und unbekanntes Modell.
  - Parser: Profil, Extra-Fenster, Credentials, `.claude.json`.
  - `UsageHistoryStore`: Anhängen, Aufräumen, Löschen.
  - `UsageService`: schreibt den Verlauf nur bei Ok.
- **App:**
  - VM-Flows: Menü öffnet die Info-Page, Esc führt zurück, erst Cache dann frisch, Umschalter.
  - Poller mit manuellem Tick.
  - `ControlTests`: `ChartScale` sowie jedes Chart mit Daten und leer.
  - `PageRenderTests`: Info-Page und erweitertes Kachel-Menü.
  - `ScreenshotTests`: Hell und Dunkel.
  - `ThemeTests`: neue Tokens und Icon.
