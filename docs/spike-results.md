# MCACC Spike-Ergebnisse

Ausgefüllt nach `docs/spike-checklist.md`. Ein Abschnitt pro Punkt mit *Befund* (was tatsächlich
passiert ist) und *Entscheidung* (was das für MCACC bedeutet). Am Ende die zusammengefassten
Entscheidungen D1–D7.

Datum: _____
Plattformen: ☐ Windows ☐ macOS
Claude-Desktop-Version (Windows): _____
Claude-Desktop-Version (macOS): _____
macOS-Version: _____

---

## Windows

### W1 — `--user-data-dir` honoriert

**Befund:**

**Entscheidung:**

### W1b — Speicherort des Default-Profils

**Befund:**

**Entscheidung:**

### W2 — Prozess-Snapshot

**Befund:**

**Entscheidung:**

### W3 — Env-Variable erreicht die App

**Befund:**

**Entscheidung:**

### W4 — Parallelbetrieb und Zweitstart

**Befund:**

**Entscheidung:**

### W5 — Beenden

**Befund:**

**Entscheidung:**

### W6 — Login-Deep-Link

**Befund:**

**Entscheidung:**

### W7 — Windows Terminal

**Befund:**

**`%VAR%`-Expansion:** `-d`-Ordner ☐ expandiert ☐ wörtlich · `-Command`-Skript ☐ expandiert ☐ wörtlich

**Entscheidung:**

### W8 — CLI-Konfigurationsordner

**Befund:**

**Entscheidung:**

### W9 — Usage-Endpoint

**Befund:**

**Entscheidung:**

---

## macOS

### M1 — Bundle-ID und `open`-Fähigkeiten

**Befund:**

**Entscheidung:**

### M2 / M3 — Eigenes Profil starten

**Befund:**

**Entscheidung:**

### M4 — Prozess-Snapshot

**Befund:**

**Entscheidung:**

### M5 — Beenden

**Befund:**

**Entscheidung:**

### M6 — Login-Deep-Link

**Befund:**

**Entscheidung:**

### M7 — Keychain

**Befund:**

**Entscheidung:**

### M8 — `.command`-Datei

**Befund:**

**Entscheidung:**

### M9 — Fallback ohne `--env`

**Befund:**

**Entscheidung:**

### M10 — Signierte MCACC.app (nach Task 14)

**Befund:**

**Entscheidung:**

---

## Entscheidungen

### D1 — `--user-data-dir` wird honoriert (Win/Mac)

_Grundlage: W1, W1b, M2/M3._

**Entscheidung:**

### D2 — Env erreicht Desktop (Code-Tab-Kopplung möglich?)

_Grundlage: W3, M2/M3._

**Entscheidung:**

### D3 — Login-Workaround

_Grundlage: W6, M6._

**Entscheidung:**

### D4 — Quit-Strategie

_Grundlage: W5, M5._

**Entscheidung:**

### D5 — Terminal-Befehle und Escaping ok

_Grundlage: W7, M8._

**Entscheidung:**

### D6 — macOS `open --env` verfügbar

_Grundlage: M1._

**Entscheidung:**

### D7 — Keychain-Service-Namensschema

_Grundlage: M7._

**Entscheidung:**
