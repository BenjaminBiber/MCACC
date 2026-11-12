# Claude-Limits über die Statusline mitschreiben (Backup)

Stand: 2026-11-12

## Ziel

Liefert der Usage-Endpunkt von Anthropic nichts (Token abgelaufen, 401/403, 429, Netzwerkfehler), zeigt eine
Claude-Kachel trotzdem echte Prozente – so wie Codex auf seine Session-Dateien zurückfällt. Claude Code legt die
Limits selbst nirgends auf der Platte ab; die einzige lokale Quelle ist das JSON, das es der Statusline übergibt.

## Quelle

Claude Code ruft das `statusLine`-Kommando bei Sessionstart, nach jeder Antwort und nach einem Reset auf (300 ms
entprellt) und gibt auf stdin u. a. mit:

```json
"rate_limits": {
  "five_hour": { "used_percentage": 23.5, "resets_at": 1738425600 },
  "seven_day": { "used_percentage": 41.2, "resets_at": 1738857600 }
}
```

`used_percentage` ist 0–100, `resets_at` Unix-Sekunden. Das Objekt fehlt bei API-Key-Logins und vor der ersten
Antwort einer Session; ein Fenster fehlt einzeln, sobald sein Reset vorbei ist. Die Desktop-App ruft (laut Doku
nicht belegt) keine Statusline auf – dort greift das Backup nicht.

## Bausteine

1. **Statusline-Modus von MCACC** – `MCACC --mcacc-statusline` läuft vor Single-Instance und Avalonia, liest stdin,
   schreibt bei vorhandenem `rate_limits` atomar `<Config-Ordner>/mcacc-limits.json` und gibt nichts aus. Der
   Config-Ordner ist `CLAUDE_CONFIG_DIR`, sonst `~/.claude`; das Kommando enthält also nichts Account-Spezifisches
   und überlebt das Kopieren der `settings.json`. Exit-Code immer 0, Fehler werden verschluckt.
2. **`ClaudeLimitsFile`** (Core) – Format der Datei: Zeitpunkt der Aufnahme plus die zwei Fenster. Parsen des
   Statusline-JSON, Schreiben und Lesen als `UsageSnapshot` mit `UsageSource.LocalFile`.
3. **`StatusLineSettings`** (Core) – trägt in einer `settings.json` den Eintrag
   `"statusLine": { "type": "command", "command": "<exe> --mcacc-statusline" }` ein oder entfernt ihn. Alle anderen
   Schlüssel bleiben. Ein fremder `statusLine` wird nie überschrieben. Eine Datei, die kein JSON-Objekt ist, bleibt
   unangetastet. Der Exe-Pfad steht mit `/` und ohne Anführungszeichen, damit Git Bash und PowerShell ihn gleich
   lesen; nur ein Pfad mit Leerzeichen bekommt einfache Anführungszeichen (dann nur noch für Git Bash/sh).
4. **`StatusLineSync`** (App) – wendet den Schalter auf alle Claude-Accounts an: beim Start, beim Umschalten und nach
   dem Anlegen eines Accounts. Ein falscher Exe-Pfad in einem eigenen Eintrag wird beim Start korrigiert. Liefert
   die Accounts zurück, die wegen einer fremden Statusline übersprungen wurden.
5. **Schalter** in den Einstellungen, `AppSettings.RecordClaudeLimits`, Standard aus. Darunter der Hinweis auf die
   übersprungenen Accounts.

## Rückfall in `UsageService`

Endet der Abruf mit `Expired`, `Unauthorized`, `RateLimited` oder `Error` (auch beim Profil-Check), liest der
Dienst `mcacc-limits.json` aus dem Config-Ordner des Accounts:

- Keine Datei → wie bisher (Zustand + letzter Stand).
- Datei neuer als der letzte gespeicherte Stand → speichern (Cache + Verlauf), Zustand `Stale`.
- Letzter gespeicherter Stand stammt selbst aus der Datei und ist gleich alt → `Stale` mit diesem Stand, ohne erneut
  zu speichern.
- Letzter Stand ist ein neuerer API-Wert → wie bisher.

Kein Rückfall bei `NotLoggedIn` und `WrongAccount`. Die Kachel zeigt bei `Stale` „Stand von …“.

## Tests

- `ClaudeLimitsFile`: vollständiges JSON, ein Fenster fehlt, kein `rate_limits`, kaputtes JSON, Rundreise.
- `StatusLineSettings`: eintragen in leere/fehlende/bestehende Datei, entfernen, fremde Statusline, Pfad korrigieren,
  übrige Schlüssel bleiben, kaputte Datei bleibt.
- `UsageService`: Rückfall bei abgelaufenem Token und HTTP-Fehler, keine Datei, älter als der API-Stand, kein
  doppelter Verlaufseintrag, kein Rückfall bei `WrongAccount`.
- `SettingsViewModel`: Schalter speichert und wendet an.
