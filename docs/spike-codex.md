# Spike: Codex als zweiter Provider

Ergänzung zu `docs/spike-results.md`, erhoben für MCA-0017 am 2026-10-06 auf Windows 11
(Codex-CLI aus `%LOCALAPPDATA%\Programs\OpenAI\Codex\bin`, Desktop-Paket
`OpenAI.Codex_26.930.3930.0_x64__2p2nqsd0c76g0`). Ein Abschnitt pro Punkt mit *Befund* und
*Entscheidung*. Persönliche Kennungen in den Beispielantworten sind durch Platzhalter ersetzt.

---

## C1 — Profil-Isolation der CLI

**Befund:** Die Codex-CLI liest ihr Zuhause aus `CODEX_HOME` (im Binary 75 Vorkommen; in der
erzeugten `config.toml` wird die Variable selbst gesetzt). Standard ist `~/.codex`. Daneben
existiert `CODEX_SQLITE_HOME`; dessen Standard liegt unter `CODEX_HOME` — die SQLite-Dateien
(`logs_2.sqlite`, `state_5.sqlite`, …) liegen tatsächlich direkt in `~/.codex`.

**Entscheidung:** `CODEX_HOME` ist das Gegenstück zu `CLAUDE_CONFIG_DIR`. Beim Start muss
`CODEX_SQLITE_HOME` **entfernt** werden, sonst zeigt der Zustand aus dem Profil heraus.

## C2 — Credentials und Identität auf der Platte

**Befund:** `<CODEX_HOME>/auth.json` mit
`{ "auth_mode", "last_refresh", "tokens": { "access_token", "refresh_token", "id_token", "account_id" } }`.
Das `id_token` ist ein JWT. Kein Keychain-Zugriff unter Windows. Die Konfiguration ist
**TOML** (`config.toml`), nicht JSON.

**Entscheidung:** Ein Datei-Credential-Leser genügt (Windows). Für macOS ungeprüft —
Keychain oder `auth.json`, siehe C7.

## C3 — Rate-Limits in den Session-Dateien

**Befund:** Die Rollout-Dateien `<CODEX_HOME>/sessions/<yyyy>/<MM>/<dd>/rollout-*.jsonl`
enthalten Zeilen mit

```json
"rate_limits":{"limit_id":"codex","limit_name":null,
  "primary":{"used_percent":0.0,"window_minutes":300,"resets_at":1790854085},
  "secondary":{"used_percent":0.0,"window_minutes":10080,"resets_at":1791440885},
  "credits":{"has_credits":false,"unlimited":false,"balance":"0"}}
```

`window_minutes` 300 = 5 h, 10080 = 7 Tage; `resets_at` in Unix-Sekunden.

**Entscheidung:** Taugt als Fallback ohne Netz. Die Werte sind nur so aktuell wie die letzte
Codex-Sitzung, müssen also als Stand gekennzeichnet werden.

## C4 — Live-Abfrage der Limits (die eigentliche Spike-Frage)

**Befund:** **Es gibt einen Endpunkt.** `GET https://chatgpt.com/backend-api/wham/usage`
antwortet mit 200. Derselbe Rumpf kommt unter `GET https://chatgpt.com/backend-api/codex/usage`.
Header: `Authorization: Bearer <access_token aus auth.json>`,
`chatgpt-account-id: <tokens.account_id>`, `originator: codex_cli_rs`.

```json
{
  "user_id": "user-…", "account_id": "…", "email": "…", "plan_type": "plus",
  "rate_limit": {
    "allowed": true, "limit_reached": false,
    "primary_window":   { "used_percent": 0, "limit_window_seconds": 18000,  "reset_after_seconds": 18000,  "reset_at": 1791291393 },
    "secondary_window": { "used_percent": 0, "limit_window_seconds": 604800, "reset_after_seconds": 604800, "reset_at": 1791878193 }
  },
  "code_review_rate_limit": null, "additional_rate_limits": null,
  "model_usage": { "gpt-6-astra": { "available": true, "available_at": null, "credits_would_enable": false } },
  "credits": { "has_credits": false, "unlimited": false, "overage_limit_reached": false, "balance": "0",
               "approx_local_messages": [0,0], "approx_cloud_messages": [0,0] },
  "spend_control": { "reached": false, "individual_limit": null },
  "rate_limit_reached_type": null, "promo": null,
  "rate_limit_reset_credits": { "available_count": 2, "applicable_available_count": 0 }
}
```

**Achtung auf die Einheit:** hier `limit_window_seconds` (18000 / 604800), in den
Rollout-Dateien `window_minutes` (300 / 10080). Dieselben Fenster, zwei Einheiten.

**Entscheidung:** Der Live-Pfad ist der Hauptpfad, `CodexRateLimitsHttp` wird echt. Die
Zuordnung auf `FiveHour`/`SevenDay` geschieht über die Fensterlänge, nicht über die Position,
und muss beide Einheiten verstehen. Der Endpunkt ist **undokumentiert** — er kann jederzeit
brechen, deshalb bleibt der Datei-Fallback aus C3 bestehen.

## C5 — Identität und Fremd-Token-Erkennung

**Befund:** Dieselbe Antwort aus C4 nennt `email`, `plan_type`, `user_id` und `account_id` —
also das Gegenstück zu Claudes `/api/oauth/profile`.

**Entscheidung:** Die Prüfung „Token gehört zu einem anderen Konto als das Profil nennt"
(heute `UsageService.IsSameAccount`) ist für Codex **doch möglich** und wird zur Parität
mitgenommen: `email`/`account_id` aus `auth.json` gegen die der Antwort. Damit korrigiert
sich die Plan-Annahme, das entfalle bei Codex.

## C6 — Statistik

**Befund:** `GET https://chatgpt.com/backend-api/wham/profiles/me` antwortet mit 200 und
liefert serverseitige Nutzungszahlen: `lifetime_tokens`, `peak_daily_tokens`,
`current_streak_days`, `total_threads`, `daily_usage_buckets` und
`cumulative_daily_usage_buckets` (je `start_date` + `tokens`), `weekly_usage_buckets`,
`top_invocations` (Skills/Plugins mit Zählern), `fast_mode_usage_percentage`,
`most_used_reasoning_effort`.

**Entscheidung:** Die Tageswerte für die Info-Seite kommen von dort — kein Scannen nötig und
unabhängig davon, ob lokal Rollout-Dateien liegen. **Nicht** enthalten sind Tokens pro Modell
und pro Projekt; dafür bleibt das lokale Lesen der Rollout-Dateien nötig
(`token_count`-Ereignisse, `cwd` je Sitzung für die `excludedProjects`-Einstellung).

## C7 — Desktop-App

**Befund:** Die App heißt im Startmenü **„ChatGPT"**, ist aber das MSIX-Paket
`OpenAI.Codex_2p2nqsd0c76g0` (`app/ChatGPT.exe`). Sie ist Chromium-basiert (`chrome.dll`
154.0.8037.98, `owl-shell-runtime.json`); `chrome.dll` enthält `user-data-dir` **und**
`profile-directory`. Das `AppxManifest.xml` registriert genau ein Protokoll: `codex`
(im CLI-Binary z. B. `codex://threads`). macOS-Bundle: `com.openai.codex` (Kanäle `.alpha`/`.beta`/`.dev`/`.nightly`/`.agent`).
Daten liegen unter
`%LOCALAPPDATA%\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Roaming\Codex`.
Die CLI kennt `codex app [PATH]`, aber **ohne** `--user-data-dir`-Schalter; der Schalter
gehört der Chromium-Laufzeit, nicht der CLI. `codex app` öffnet bei fehlender App einen
Installer.

**Entscheidung:** Baugleich zu Claude Desktop — dieselbe Isolation über `--user-data-dir`,
dieselbe Deep-Link-Mechanik mit `codex://`. Gestartet wird das MSIX-Paket direkt (AppId `App`),
nicht über `codex app`. **Noch nicht nachgewiesen:** dass die App ihren Anmeldezustand
wirklich im `--user-data-dir` hält — das zeigt erst ein Start aus zwei Profilen (MCA-0022).
Der Windows-Prozess heißt `ChatGPT.exe`, nicht `Codex.exe`.

## C8 — Umgebungsvariablen, die beim Start weg müssen

**Befund:** Im Binary gefunden: `CODEX_SANDBOX`, `CODEX_SANDBOX_NETWORK_DISABLED`,
`CODEX_SESSION_ID`, `CODEX_NON_INTERACTIVE`, `CODEX_ACCESS_TOKEN`,
`CODEX_PERMISSION_PROFILE`, `CODEX_INTERNAL_ORIGINATOR_OVERRIDE`, dazu die
Proxy-Schnappschüsse `CODEX_NETWORK_PROXY_*` und `CODEX_SQLITE_HOME` (siehe C1).
Ferner `CODEX_MANAGED_BY_NPM|BUN|PNPM|VITE_PLUS` — die CLI kann also auch über einen
Paketmanager installiert sein und dann anderswo liegen.

**Entscheidung:** Diese Variablen wandern in `EnvironmentScrub.Always`. Der Fundort des
Binaries bleibt die `PATH`-Suche mit `%LOCALAPPDATA%\Programs\OpenAI\Codex\bin` als
zusätzlichem Kandidaten.

## C9 — Nicht geprüft

- **macOS insgesamt:** Ordnername unter `~/Library/Application Support`, `auth.json` gegen
  Keychain, Fundort des `codex`-Binaries. Braucht eine Sitzung auf dem Mac.
- **Isolation der Desktop-App** über `--user-data-dir` in der Praxis (siehe C7).
- **Lebensdauer des `access_token`** und ob MCACC ihn je erneuern müsste. MCACC erneuert bei
  Claude nichts und soll das auch hier nicht tun: ein abgelaufenes Token lässt den letzten
  Wert stehen.

---

## Nachtrag aus dem zweiten Durchlauf (gleicher Tag)

### C7a — Korrektur zur macOS-Bundle-ID

**Befund:** `com.openai.codex.user` im CLI-Binary ist **keine** Bundle-ID, sondern der Anfang
von `com.openai.codex.user-verification.plugin-service.v1`. Im Desktop-Paket (`app.asar`)
stehen die echten: `com.openai.codex` plus die Kanäle `.alpha`, `.beta`, `.dev`, `.nightly`,
`.agent`. Daneben liegen dort `com.openai.chat` und `com.openai.chatgpt` — die **separate**
ChatGPT-App, nicht diese.

**Entscheidung:** `MacBundleId = "com.openai.codex"`.

### C7b — Die Desktop-App ist von sich aus mehrprofilfähig

**Befund:** Im `app.asar`:

- `resolveCodexHome()` endet auf `return process.env.CODEX_HOME ?? join(homedir(), '.codex')`
  — die App respektiert `CODEX_HOME` also genauso wie die CLI.
- Der Electron-Benutzerdatenpfad kommt aus `env.CODEX_ELECTRON_USER_DATA_PATH?.trim()`, sonst
  `join(appData, 'Codex')`. `owl-app.ini` nennt `UserDataDirectoryName=Codex`.
- Der **app-eigene** Zweitprofil-Start (macOS-Zweig) lautet wörtlich
  `/usr/bin/open -n --env CODEX_HOME=<h> --env CODEX_ELECTRON_USER_DATA_PATH=<u> <app> --args --user-data-dir=<u>`
  — strukturgleich zu `DesktopLaunchPlanner.PlanMac`.
- Die Single-Instance-Sperre wird pro Benutzerdatenverzeichnis vergeben.

**Entscheidung:** Es sind **zwei** Variablen plus der Schalter nötig, nicht nur
`--user-data-dir`: `CODEX_HOME` (Agent-Zuhause) **und** `CODEX_ELECTRON_USER_DATA_PATH`
(Electron-Profil) **und** `--user-data-dir` auf denselben Pfad. Auf macOS ist die Isolation
damit praktisch gelöst.

### C7c — Offenes Risiko: der Windows-Start

**Befund:** MCACC startet Claude Desktop über den App-Ausführungsalias
`%LOCALAPPDATA%\Microsoft\WindowsApps\claude-desktop.exe`. Das Codex-MSIX deklariert Aliase
**nur** für `codex-chrome-native-host.exe` und `codex-core-command-runner.exe` — **keinen für
die Haupt-App**. Damit hat der vorhandene Windows-Startweg kein Codex-Gegenstück. Die
Alternativen sind alle unbelegt: direkter Aufruf von
`…\WindowsApps\OpenAI.Codex_<ver>_x64__2p2nqsd0c76g0\app\ChatGPT.exe` verliert
wahrscheinlich die Paketidentität (und der Pfad trägt die Version, ändert sich also bei jedem
Update); `Invoke-CommandInDesktopPackage` behält die Identität, kann aber **keine
Umgebungsvariablen** setzen und ist damit für die Isolation nutzlos; `codex app` enthält
`CODEX_ELECTRON_USER_DATA_PATH` nirgends und aktiviert vermutlich die eine gemeinsame Instanz
per Protokoll.

**Entscheidung:** Der Windows-Desktop-Start für Codex ist **nicht nachgewiesen**. MCA-0022
beginnt mit einem Versuch gegen Wegwerf-Verzeichnisse; schlägt er fehl, bleibt Codex ohne
Desktop-Ziel (die Zielauswahl zeigt dann nur „Codex"), und MCA-0021, 0023, 0024, 0025, 0026
sind davon unberührt.

### C7d — `ChatGPT.exe` ist kein eindeutiger Prozessname

**Befund:** Die separate ChatGPT-App (`com.openai.chat`) heißt genauso.

**Entscheidung:** Die Prozesserkennung darf nicht über den Namen gehen, sondern über das
Pfadsegment `\WindowsApps\OpenAI.Codex_` — dieselbe Technik, die `DesktopProcessRules` für
Claude schon mit `\WindowsApps\Claude_` verwendet.

### C10 — `config.toml` enthält absolute Pfade, darunter `CODEX_HOME` selbst

**Befund:** Die echte Datei trägt u. a.

```toml
[mcp_servers.node_repl.env]
CODEX_HOME = 'C:\Users\<user>\.codex'
NODE_REPL_TRUSTED_CODE_PATHS = 'C:\Users\<user>\.codex;…'
[marketplaces.openai-bundled]
source = '\?\C:\Users\<user>\.codex\.tmp\bundled-marketplaces\openai-bundled'
[projects.'c:\users\<user>']
```

**Entscheidung:** Ein Kopieren von `config.toml` in ein neues Profil **hebt die Isolation
stillschweigend auf**, weil der eingebettete `CODEX_HOME`-Wert weiter aufs alte Zuhause zeigt.
Das Umschreiben der Pfade ist für `config.toml` daher **verpflichtend**, keine Option. Es ist
kein Fall für `SettingsPathRewriter`: TOML nutzt einfach-gequotete Literale, in denen
Backslashes **nicht** escaped werden, und das `\?\`-Langpfad-Präfix muss überleben.

### C11 — Tokenzählung zählt anders als bei Anthropic

**Befund:** In `token_usage_record`-Zeilen gilt
`total_tokens = input_tokens + output_tokens`, wobei `cached_input_tokens`
**in `input_tokens` enthalten** ist (Beispiel: 29084 = 29013 + 71 bei
`cached_input_tokens` 28800). Bei Anthropic liegen die Cache-Werte *neben*
`input_tokens`.

**Entscheidung:** Ein direkter Nachbau von `LogLineParser` würde bei Codex jede Sitzung
nahezu **doppelt** zählen. Richtig ist
`Input = input_tokens - cached_input_tokens - cache_write_input_tokens`,
`CacheRead = cached_input_tokens`. Die Prüfung dafür ist
`TokenCounts.Total == payload.usage.total_tokens`.

### C12 — Modell und Projekt stehen nicht auf der Verbrauchszeile

**Befund:** `token_usage_record` trägt nur `response_id`, `thread_id`, `turn_id` und `usage`.
Das Modell und das `cwd` stehen auf den vorangehenden `turn_context`- bzw.
`session_meta`-Zeilen. Das `cwd` ist ein **einfacher Pfad**, nicht Claudes verfremdeter
Projektordnername — `ProjectRoot.RepositoryOf` greift also direkt.

**Entscheidung:** Der Codex-Scanner muss Zustand pro Datei mitführen (aktuelles Modell,
aktuelles `cwd`). Das bricht die Annahme von `ClaudeLogScanner`, dass Zeilen unabhängig sind,
und kollidiert mit dem Fortsetzen ab einem gespeicherten Offset. Lösung: `LastModel` und
`LastCwd` in `IndexedLogFile` aufnehmen und `StatsIndex.CurrentVersion` erhöhen — der Scanner
baut bei einer Versionsänderung bereits neu auf.

### C13 — Unbekannte Enum-Werte in der Konfiguration

**Befund:** `JsonStringEnumConverter` **wirft** bei einem unbekannten Zeichenkettenwert. Eine
`config.json` mit einem künftigen, unbekannten Provider würde damit von
`ConfigStore.ReadOrRecover` als beschädigt in Quarantäne gestellt, statt in den
Read-only-Modus zu gehen.

**Entscheidung:** Ein toleranter Konverter für `ProviderKind` (unbekannt ⇒ `Claude`) neben dem
vorhandenen `CamelCaseEnumConverter<LaunchTarget>`. Für bereits ausgelieferte Fassungen ist
das nicht nachträglich zu heilen; es schützt künftige Provider-Erweiterungen.

### C14 — Zum Live-Endpunkt (Widerspruch aufgelöst)

**Befund:** Ein Durchlauf schloss aus dem Fehlen von `/usage`-Zeichenketten, es gebe keinen
Endpunkt. Das ist widerlegt: `/wham/usage` **steht** im Binary (siehe C4), und der Aufruf
wurde mit Status 200 und vollständigem Rumpf ausgeführt.

**Entscheidung:** C4 gilt. Die Limits sind **nicht** nur an eine Inferenz-Antwort gekoppelt;
eine Abfrage kostet keine Tokens.

### C1a — Das `CODEX_SQLITE_HOME`-Leck, nachgemessen

**Befund:** `codex doctor` mit `CODEX_HOME` auf ein Wegwerf-Profil und **ohne**
`CODEX_SQLITE_HOME` meldet `sqlite home` auf dasselbe Profil; alle Datenbanken (`state_5`,
`logs_2`, `goals_1`, `memories_1`, `memories_v2_1`, `queue_1`, `thread_history_1`) liegen
dort. Mit geerbtem `CODEX_SQLITE_HOME = C:\Users\<user>\.codex` meldet derselbe Aufruf
`sqlite home` auf das **echte** Zuhause, und alle Datenbanken werden dort gefunden — während
die Konfiguration im Profil bleibt.

**Entscheidung:** Bestätigt C1. Ohne Entfernen von `CODEX_SQLITE_HOME` wäre ein Profil halb
isoliert: eigene Konfiguration, gemeinsamer Zustand und gemeinsame Verlaufsdatenbanken. Die
Variable steht deshalb in `EnvironmentScrub.Always`.

**Gegenprobe des erzeugten Startskripts:** Das von `CmdBatchBuilder` erzeugte Skript wurde
mit absichtlich vorbelegten `CLAUDE_CONFIG_DIR` und `CODEX_SQLITE_HOME` ausgeführt (letzte
Zeile zu `codex login status` geändert, um es nicht-interaktiv zu machen). Im Prozess war
`CODEX_HOME` auf das Profil gesetzt, `CLAUDE_CONFIG_DIR` und `CODEX_SQLITE_HOME` leer, und
`codex login status` meldete für das frische Profil `Not logged in`, während
`~/.codex/auth.json` unberührt blieb.

### C7e — Der Desktop-Start, nachgemessen

**Befund:** `app/ChatGPT.exe` aus dem MSIX-Verzeichnis wurde direkt gestartet, mit
`CODEX_HOME` und `CODEX_ELECTRON_USER_DATA_PATH` auf Wegwerf-Ordner und
`--user-data-dir` auf denselben Electron-Pfad. Ergebnis:

- Die App startet als **zweite** Instanz neben der laufenden (12 → 20 Prozesse).
- Das Wegwerf-`--user-data-dir` füllt sich mit einem vollständigen Chromium-Profil
  (`Default`, `Local State`, `Crashpad`, …).
- Das Wegwerf-`CODEX_HOME` füllt sich mit einem vollständigen Codex-Zuhause: eigene
  `config.toml`, eigene `installation_id` (andere als die echte), eigene
  `goals_1.sqlite`/`logs_2.sqlite`/`memories_1.sqlite`.
- **Kein `auth.json`** im Testprofil: die Anmeldung wird nicht übernommen, das Profil ist
  abgemeldet. Das echte `~/.codex/auth.json` blieb unberührt.
- Die Testinstanz ließ sich über die Prozesse beenden, deren Kommandozeile den
  Wegwerf-Pfad trägt; die Instanz des Nutzers lief unverändert weiter.
- Größe eines frischen Desktop-Profils: rund **600 MB**.

**Entscheidung:** Die Profil-Isolation der Desktop-App funktioniert und braucht **drei**
Angaben zusammen: `CODEX_HOME`, `CODEX_ELECTRON_USER_DATA_PATH` und `--user-data-dir`.

### C7f — Offen: wie MCACC die App unter Windows findet

**Befund:** MCACC findet Claude Desktop über den App-Ausführungsalias
`%LOCALAPPDATA%\Microsoft\WindowsApps\claude-desktop.exe`. Für Codex gibt es nur die Aliase
`codex-chrome-native-host.exe` und `codex-core-command-runner.exe` — **keinen für die
Haupt-App**. Das Verzeichnis `C:\Program Files\WindowsApps` lässt sich als normaler Nutzer
**nicht auflisten** (`UnauthorizedAccessException`), ein Glob über den versionierten
Ordnernamen scheidet also aus. Ein klassischer `shell\open\command`-Eintrag für `codex://`
existiert unter `HKCU\Software\Classes` nicht; das Protokoll ist über das MSIX-Manifest
registriert. Zuverlässig ist dagegen
`(Get-AppxPackage -Name OpenAI.Codex).InstallLocation` →
`C:\Program Files\WindowsApps\OpenAI.Codex_<version>_x64__2p2nqsd0c76g0`.

**Entscheidung:** Der Windows-Locator muss den Installationsort über den Paketmanager
erfragen, nicht über einen festen Pfad. Ohne neue Abhängigkeit geht das über den
vorhandenen `IProcessRunner` mit
`powershell -NoProfile -Command "(Get-AppxPackage -Name OpenAI.Codex).InstallLocation"` —
dasselbe Muster, das `MacDesktopInstallationLocator` schon für macOS benutzt. Der Pfad trägt
die Version und ändert sich bei jedem Update, darf also nicht zwischengespeichert werden.
