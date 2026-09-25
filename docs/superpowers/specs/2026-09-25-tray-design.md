# MCACC – Tray- und Menüleisten-App

Ticket: MCA-0010 · Ergänzt `2026-09-23-mcacc-design.md`.

## Context

Heute läuft MCACC als kurzlebiges Fenster: Konto wählen, Ziel wählen, starten. Danach beendet sich die App (`CloseAfterLaunch`, Standard `true`). Wer den Account wechseln oder die Limits sehen will, muss MCACC jedes Mal neu starten.

MCACC soll deshalb dauerhaft als Icon im Windows-Infobereich bzw. in der macOS-Menüleiste leben. Von dort lassen sich Konten direkt starten und ihre Limits ablesen, ohne das Fenster zu öffnen.

## Entscheidungen (mit Benjamin geklärt, 2026-09-25)

| Thema | Entscheidung |
|---|---|
| Modus | Das Icon ist immer aktiv, eine Einstellung zum Abschalten gibt es nicht. Das Fenster zu schließen versteckt es nur. Beendet wird über „MCACC beenden“ im Tray-Menü oder mit Cmd+Q. |
| Menü | Konten mit Direktstart: pro Konto ein Untermenü mit Auslastung und „läuft“-Status, darin „Claude Desktop starten“ und „Claude Code starten“. Dazu „MCACC öffnen“, „Limits aktualisieren“, „Einstellungen …“ und „MCACC beenden“. |
| macOS-Dock | Dynamisch: Das Dock-Icon ist nur zu sehen, solange das Fenster offen ist. `LSUIElement` wird nicht gesetzt. |
| Zweiter Start | Er holt das Fenster der laufenden Instanz nach vorne, über eine lokale Named Pipe (unter macOS ein Unix-Socket). |
| Nicht im Umfang | Autostart bei der Anmeldung; Prozentzahlen, die ins Icon gezeichnet werden. |

## Lebenszyklus

- `ShutdownMode.OnExplicitShutdown`. Der Schließen-Knopf, Alt+F4 und Esc auf der Account-Auswahl verstecken das Fenster.
- `CloseAfterLaunch` behält den JSON-Schlüssel. Die Einstellung heißt jetzt „Nach dem Start ausblenden“ und versteckt das Fenster, statt die App zu beenden.
- Ist das Fenster versteckt und läuft nichts, springt es auf die Account-Auswahl zurück. Banner und offene Dialoge sind dann weg. Beim nächsten Öffnen sieht es aus wie nach einem Neustart.
- Unter macOS öffnet ein Klick auf das Dock-Icon (Reopen) das Fenster.

## Starten aus dem Tray

Die Start-Logik zieht aus der Zielauswahl in einen `LaunchCoordinator` um, den die Zielauswahl und das Tray gemeinsam nutzen.

- Ein Start ohne Probleme bleibt unsichtbar.
- Ein Konflikt-Dialog, ein Fehler oder ein Hinweis holt zuerst das Fenster nach vorne.

## Tray-Menü

```
MCACC öffnen
─────
Privat · 5h 42 % · Woche 10 %      ▸  5h: 42 %, Reset in 2 Std.  (deaktiviert)
Arbeit · läuft                     ▸  Woche: 10 %, …             (deaktiviert)
                                      ─────
                                      Claude Desktop starten
                                      Claude Code starten
─────
Limits aktualisieren
Einstellungen …
─────
MCACC beenden
```

- Das zuletzt benutzte Konto steht oben, die übrigen folgen in der Reihenfolge der Konfiguration.
- Das Menü wird neu aufgebaut, sobald der Poller neue Limits liefert, sich Konten ändern, ein Start stattfindet oder das Fenster versteckt wird.
- Windows: Ein Linksklick auf das Icon öffnet das Fenster. Der Tooltip zeigt das letzte Konto mit seiner Auslastung, gekürzt auf 127 Zeichen.
- Icon unter Windows: `mcacc.ico`.
- Icon unter macOS: ein monochromes Template-Icon, zur Laufzeit aus dem Material-Glyph `switch_account` gerendert. Es passt sich an helle und dunkle Menüleisten an.

## Tests

- **`TrayMenuModel`:** reine Funktion, per Unit-Test geprüft.
- **Neue Formatter:** in `TextsTests`.
- **`LaunchCoordinator`:** übernimmt die Fälle der Zielauswahl und prüft zusätzlich, wann das Fenster gezeigt wird.
- **Headless-Test:** Schließen versteckt nur.
- **Pipe:** Round-Trip-Test.
- **macOS:** Menüleiste, Dock und Reopen prüft Benjamin von Hand.
