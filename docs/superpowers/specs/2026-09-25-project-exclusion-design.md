# MCACC – Projekte von der Info-Statistik ausschließen

Ticket: MCA-0012 · Ergänzt die Info-Seite aus MCA-0009.

## Context

Die Info-Seite rechnet die Claude-Code-Nutzung aus den Session-Logs zusammen. Jede Session gehört dabei über ihr Arbeitsverzeichnis zu einem Projekt: zur Repo-Wurzel, mit Worktrees und Unterordnern zusammengefasst (`ProjectRoot.RepositoryOf`). Manche Projekte sollen in dieser Statistik nicht auftauchen, etwa Spielwiesen, fremde Repos oder Testordner.

## Entscheidungen (mit Benjamin geklärt, 2026-09-25)

| Thema | Entscheidung |
|---|---|
| Wirkung | Ganz herausrechnen: Ein ausgeschlossenes Projekt zählt auf der Info-Seite nirgends – nicht in Tokens, Kosten, Sessions, Nachrichten, aktiven Tagen, Tagesbalken, Modellen, Token-Arten, Top-Projekten und Kalender. |
| Auswahl | Aus einer Liste der erkannten Projekte wählen. Muster und Wildcards gibt es nicht. |
| Geltung | Eine Liste für alle Accounts. |
| Ort | Nur in den Einstellungen, nicht direkt auf der Info-Seite. |

## Speicherung

- `AppSettings.ExcludedProjects` (`IReadOnlyList<string>?`, JSON `excludedProjects`) ist die Liste der ausgeschlossenen Projektpfade.
- Leer heißt `null`, und der Schlüssel fehlt dann in `config.json`. Alte Konfigurationen laden so unverändert, und die Werte-Gleichheit des Records bleibt für den Standard erhalten.
- Ein Eintrag ist der Projektpfad, wie ihn die Info-Seite bildet. Verglichen wird ohne Groß-/Kleinschreibung und ohne abschließendes `\` oder `/`.

## Herausrechnen

- `ProjectFilter` (Core) ist eine reine Funktion. Sie nimmt die Token-Einträge, die Zuordnung Arbeitsverzeichnis → Projekt und die Ausschlussliste und gibt die Einträge der übrigen Projekte zurück.
- Die Info-Seite filtert damit vor `AccountStatsQuery.Compute` und `AccountStatsQuery.Daily`. An der Abfrage selbst ändert sich nichts.

## Kalender

`CalendarBackfill` schätzt Tage, deren Logs Claude Code schon gelöscht hat, aus Claudes Tageszusammenfassung. Diese Zusammenfassung kennt keine Projekte.

- Ob ein Tag als unvollständig gilt und wie groß der Umrechnungsfaktor ist, bestimmen weiter die **ungefilterten** Zahlen. Ein Tag wird also nicht zum Schätz-Tag, nur weil ein ausgeschlossenes Projekt fehlt.
- Ein Tag mit Logs zeigt die gefilterten Tokens.
- Ein geschätzter Tag wird mit dem Anteil skaliert, den die berücksichtigten Projekte an allen Tokens der Tage mit Logs im Kalenderzeitraum haben. Ohne Tage mit Logs ist der Anteil 1.

## Einstellungen

- Der neue Abschnitt „Projekte in der Statistik“ steht unter „Nach dem Start ausblenden“.
- Die Seite liest beim Öffnen im Hintergrund die Logs aller Accounts. Das geht über den vorhandenen Index, beim zweiten Mal also schnell. Bis dahin steht dort „Projekte werden gelesen …“, und ein Fehler zeigt „Die Projekte ließen sich nicht lesen.“
- Je Projekt gibt es eine Zeile mit Häkchen („berücksichtigen“), Ordnername, Pfad (grau) und Tokens insgesamt über alle Accounts. Sortiert wird nach Tokens, das größte Projekt oben.
- Ein ausgeschlossenes Projekt, das in keinem Log mehr vorkommt, steht ohne Tokenzahl am Ende, damit es sich wieder einschließen lässt.
- Ein Klick auf das Häkchen speichert sofort, wie die übrigen Einstellungen.

## Info-Seite

- Ist mindestens ein Projekt ausgeschlossen, das in den Logs dieses Accounts vorkommt, steht unter der Überschrift „Claude Code Nutzung“ die graue Zeile „Ohne 2 ausgeschlossene Projekte“ (bzw. „Ohne 1 ausgeschlossenes Projekt“).
- Kehrt man von den Einstellungen zur Info-Seite zurück, rechnet sie mit der aktuellen Liste neu, ohne die Logs erneut zu lesen.
- Die Kontozeile „Projekte“ ist Claudes eigene Zahl aus `.claude.json` und bleibt unverändert.

## Tests

- Core:
  - `ProjectFilter`: Groß-/Kleinschreibung, abschließender Trenner, Worktree-Pfad gehört zum Projekt, leere Liste lässt alles durch.
  - `CalendarBackfill`: Die Schätz-Entscheidung fällt auf den ungefilterten Zahlen, die Skalierung mit dem Anteil.
  - JSON: `excludedProjects` im Round-Trip; eine Konfiguration ohne den Schlüssel lädt als `null`.
- App:
  - Einstellungen: Die Liste wird geladen und sortiert, ein Klick speichert, das Wiedereinschließen des letzten Projekts speichert `null`, verwaiste Einträge bleiben stehen.
  - Info-Seite: Summen und Top-Projekte ohne das Projekt, die Hinweiszeile, die Neuberechnung beim Zurückkehren.
