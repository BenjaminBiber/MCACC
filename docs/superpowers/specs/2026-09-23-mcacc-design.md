# MCACC – Claude Account Switcher (Windows + macOS)

## Context

Benjamin hat einen Firmen- und einen Privat-Account bei Claude. Claude Desktop und Claude Code können nicht komfortabel zwischen Accounts wechseln.

Der Trick aus dem Reddit-Post: Claude Desktop ist Electron. Mit `--user-data-dir=<Ordner>` bekommt jede Instanz ein eigenes Profil mit eigenem Login. Claude Code lässt sich über `CLAUDE_CONFIG_DIR` genauso umleiten.

MCACC wird ein kleiner Launcher:
1. **Account-Auswahl:** Kacheln aller Accounts mit Login-E-Mail und Nutzungslimits (5h/Woche).
2. **Zielauswahl:** zwei Kacheln, „Claude Desktop“ und „Claude Code“.
   - Desktop startet die App mit dem Profil des Accounts und beendet eine laufende Instanz eines anderen Accounts nach Rückfrage.
   - Code öffnet ein Terminal mit `claude` im Home-Ordner.

Design: Claude-Theme, minimalistisch, Akzent Claude-Orange `#D97757`, Hell/Dunkel nach System, UI auf Deutsch. Das Mockup ist abgestimmt.

## Entscheidungen (mit Benjamin geklärt)

| Thema | Entscheidung |
|---|---|
| Stack | Avalonia 12 auf .NET 10, MVVM (CommunityToolkit.Mvvm), eine Codebasis Win + Mac |
| Bestand | Das heutige Setup wird Account „Standard“ (umbenennbar): Desktop ohne Flag, Claude Code ohne `CLAUDE_CONFIG_DIR`, kein neuer Login |
| Claude-Code-Config | Pro Account getrennt. Beim Anlegen optional kopieren aus einem anderen Account: CLAUDE.md, settings.json, agents, skills, commands, scripts, Desktop-MCP-Config |
| Desktop läuft schon | Läuft ein anderer Account → Dialog, dann beenden und neu starten. Läuft derselbe Account → nur fokussieren |
| Code-Tab im Desktop | Gekoppelt: Desktop für Account X bekommt `CLAUDE_CONFIG_DIR` von X |
| Claude Code | Startet immer im Home-Ordner. Nach `/exit` bleibt die Shell mit gesetzter Variable offen |
| Terminal | In den Einstellungen wählbar, nur installierte werden angezeigt |
| Nach dem Start | Switcher schließt sich (abschaltbar) |
| Entfernen | Standardmäßig nur aus der Liste. Checkbox „Daten auch löschen“ mit Bestätigung. Standard-Daten werden nie gelöscht |
| Nutzungslimits | Nur lesen über das Claude-Code-OAuth-Token, **kein** Token-Refresh. Sonst wird der letzte bekannte Wert angezeigt |
| Mac | Vorhanden → Build und Ad-hoc-Signatur per Skript auf dem Mac |
| Name | MCACC |

## Befunde, die das Design prägen

1. **MSIX:** Claude Desktop liegt unter `C:\Program Files\WindowsApps\Claude_…__pzs8sxrjxfjjc\app\Claude.exe`.
   - Gestartet wird über den Aufruf-Alias `%LOCALAPPDATA%\Microsoft\WindowsApps\claude-desktop.exe`, der Argumente durchreicht.
   - Fallback ist Squirrel: `%LOCALAPPDATA%\AnthropicClaude\claude.exe`.
2. **Dateisystem-Virtualisierung:** Das Paket virtualisiert AppData-Schreibzugriffe. Deshalb liegen alle Profilordner unter **`~/.mcacc/`**, außerhalb von AppData.
   - Die echten Standard-Desktopdaten können in `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude` liegen.
   - Das ist relevant fürs Kopieren von `claude_desktop_config.json`: zuerst dort suchen, dann in `%APPDATA%\Claude`.
3. **Prozesserkennung:** Desktop- *und* CLI-Prozesse heißen `claude.exe`.
   - Desktop wird am Pfad erkannt (`\WindowsApps\Claude_*\app\Claude.exe` bzw. `AnthropicClaude\app-*`), der Hauptprozess daran, dass die Kommandozeile kein `--type=` enthält.
   - **Der Hauptprozess trägt `--user-data-dir` nicht, die Kindprozesse schon.** Der Account wird deshalb in dieser Reihenfolge bestimmt: Hauptprozess → erstes Kind mit Flag → Standard.
   - Code-Tab-CLIs sind Kinder von Desktop und enden beim Beenden mit. Ein `claude` im Terminal wird nie angefasst.
4. **Login-Deeplink:** `claude://` ist in HKCU auf `Claude.exe "%1"` ohne Flag registriert. Ein Browser-SSO-Login eines Zusatzprofils landet unter Windows vermutlich im Standardprofil. Das wird im Spike geprüft, siehe Risiken.
5. **Diese Session läuft im MSIX-Container** (Code-Tab von Claude Desktop). Das hat zwei Folgen:
   - **MCACC nie aus dem Desktop-Code-Tab heraus testen.** Das Dateisystem ist dort virtualisiert, und das Beenden von Desktop killt die Session. Die Implementierung und die Tests laufen deshalb in einer eigenständigen `claude`-Session im Windows Terminal, und MCACC wird aus Rider oder dem Explorer gestartet.
6. **`~/.claude.json`:** Die Datei hat Keys, die sich nur in der Groß-/Kleinschreibung unterscheiden. MCACC liest nur mit `JsonDocument` gezielt `oauthAccount` (E-Mail, Organisation), mit `FileShare.ReadWrite`, und **schreibt nie**.
7. **Usage-Endpoint (inoffiziell):** `GET https://api.anthropic.com/api/oauth/usage` mit `Authorization: Bearer <accessToken>` und `anthropic-beta: oauth-2025-04-20`.
   - Liefert `five_hour` / `seven_day` mit `utilization` (0–100) und `resets_at`.
   - Ist ratenbegrenzt (429).
   - Token-Quelle: Windows `<claudeDir>/.credentials.json` → `claudeAiOauth.accessToken` / `expiresAt` (ms); macOS Schlüsselbund „Claude Code-credentials[-<hash>]“.
8. **Installierte Terminals hier:** `pwsh` ist nicht installiert, Windows Terminal 1.24 ist vorhanden, `claude` liegt im WinGet-Pfad.
9. **Icons:** `Material.Icons.Avalonia` bringt Pictogrammers-MDI, nicht Googles Set. Gemäß Benjamins Vorgabe („Google Material Icons“) wird stattdessen der Google-Font **Material Icons Round** (OTF, Apache-2.0) eingebettet und über ein kleines `MIcon`-Control mit Codepoints genutzt.

## Datenablage

```
~/.mcacc/                         (Windows: %USERPROFILE%\.mcacc)
  config.json (+ .bak, .corrupt-<ts>)
  usage-cache.json                letzte bekannte Limits je Account (ohne Tokens)
  profiles/<slug>/desktop/        --user-data-dir
  profiles/<slug>/claude/         CLAUDE_CONFIG_DIR
  launch/<slug>.cmd | .command    bei jedem Start neu generiert
  logs/mcacc-YYYYMMDD.log         7 Tage; Kommandos und Env-Namen, nie Tokens
```

```json
{ "version": 1,
  "accounts": [
    { "id": "standard", "name": "Standard", "color": "#D97757", "isDefault": true },
    { "id": "privat", "name": "Privat", "color": "#6A9BCC", "isDefault": false } ],
  "settings": { "terminal": null, "windowsTerminalShell": "auto", "closeAfterLaunch": true,
                "lastAccountId": "privat", "lastTarget": "desktop" } }
```

- **`id`:** Die `id` ist der Slug und bleibt nach dem Anlegen unverändert. Beim Umbenennen ändert sich nur der Name. Pfade werden aus der `id` abgeleitet.
- **Atomares Speichern:** zuerst `tmp` schreiben und flushen, dann die alte Datei als `.bak` sichern, dann `File.Move(overwrite)`.
- **Laden:**
  - Bei einem Parse-Fehler wird die `.bak` genutzt und die kaputte Datei quarantänisiert.
  - Eine neuere `version` wird nur lesend geöffnet.
  - Ein Validator stellt sicher: genau ein Default-Account, eindeutige IDs.
- **Root überschreibbar:** Mit der Variable `MCACC_HOME` lässt sich der Root für Tests umbiegen.

## Architektur

```
MCACC.slnx · global.json (10.0.301) · Directory.Build.props (net10.0, Nullable, TreatWarningsAsErrors)
Directory.Packages.props:
  Avalonia (+.Desktop, .Themes.Fluent, .Fonts.Inter) 12.1.3
  CommunityToolkit.Mvvm 8.4.2
  System.Management 10.0.12
  xunit.v3 3.2.2 + Avalonia.Headless.XUnit 12.1.3
  Shouldly 4.3.0
src/Mcacc.Core/        UI-frei, startet keine Prozesse, reine Logik + Datei-IO
  Model/        Account, AppConfig, AppSettings, TerminalId, LaunchTarget, UsageSnapshot, ClaudeLoginInfo, AccountPalette
  Paths/        McaccPaths, AccountPathResolver (PathStyle Win/Posix → auf jedem OS testbar), PathComparer
  Config/       ConfigStore, ConfigValidator, DefaultConfigFactory, JsonContext (Source-Gen)
  Accounts/     SlugGenerator (ä→ae…, reservierte Namen, Kollision -2), AccountNameValidator, AccountService, ProfileDataDeleter (Guard: nur unter profiles/, nie Default)
  ConfigCopy/   CopyItem (Whitelist), ConfigCopyPlanner, ConfigCopier, SettingsPathRewriter
  ClaudeCode/   ClaudeJsonLoginReader, CredentialsParser
  Usage/        UsageResponseParser, UsageService (Cache, ≥ 60 s Abstand, Status Ok/Expired/RateLimited/NoLogin/Error), IUsageHttp, ICredentialSource
  Desktop/      DesktopProcessRules, DesktopProcessClassifier, DesktopInstanceMatcher, DesktopLaunchPlanner(Win/Mac), DesktopLaunchService (Prepare → Ready|RunningSameAccount|Conflict|NotInstalled)
  Terminal/     PowerShellScriptBuilder, CmdBatchBuilder, PosixCommandFileBuilder, WindowsTerminalPlanner, MacTerminalPlanner, TerminalSelector, CodeLaunchService
  Processes/    ProcessSpec, ProcessInfo, CommandLineSwitch (--user-data-dir aus WMI/ps), PsOutputParser, EnvironmentScrub (CLAUDECODE, CLAUDE_CODE_ENTRYPOINT, CLAUDE_CODE_SSE_PORT)
  Quoting/      PowerShellQuoting (inkl. ’‘), PosixQuoting, BatchEscaping, WtEscaping
  Abstractions/ IProcessRunner, IProcessSnapshotProvider, IDesktopInstallationLocator, IDesktopInstanceTerminator, ITerminalCatalog, IExecutableLocator, IShellIntegration, IConfigStore
src/Mcacc.Platform/    einzige Stelle mit Process/WMI/libc/Keychain
  Common/   SystemProcessRunner (ArgumentList, Env set/unset), PathExecutableLocator, UsageHttpClient
  Windows/  WmiProcessSnapshotProvider (3 s Timeout), WindowsDesktopInstallationLocator, WindowsDesktopInstanceTerminator, WindowsTerminalCatalog, WindowsCredentialFileSource, WindowsShellIntegration
  MacOS/    PsProcessSnapshotProvider, MacDesktopInstallationLocator, MacDesktopInstanceTerminator (SIGTERM → SIGKILL), MacTerminalCatalog, MacKeychainCredentialSource (`security find-generic-password -w`), MacShellIntegration
  PlatformServices.cs   Factory nach OperatingSystem.IsWindows()/IsMacOS()
src/Mcacc.App/         Avalonia 12 (compiled bindings → x:DataType überall)
  Program.cs (+ Schalter --diagnose → logs/diagnose.txt) · App.axaml · CompositionRoot.cs (Handverdrahtung)
  Theme/     Tokens.axaml (Light/Dark ThemeDictionaries), Typography.axaml, Controls.axaml, LaunchTile.axaml, FontCollection
  Assets/    Fonts: Source Serif 4 (statische TTFs + OFL.txt), MaterialIconsRound-Regular.otf; mcacc.ico/png
  Controls/  LaunchTile (Avatar/Icon, Titel, Untertitel, Badge „Läuft“), UsageBar, MIcon
  Localization/Texts.cs (alle deutschen Strings)
  Services/  INavigator, IDialogService (In-Window-Overlay), IAppLifetime
  ViewModels/ MainWindow, AccountPicker, AccountTile, TargetPicker, Settings, AccountEditor, CopyItem, ConfirmDialog, RemoveAccountDialog
  Views/     MainWindow, AccountPickerView, TargetPickerView, SettingsView, AccountEditorView, ConfirmDialogView, RemoveAccountDialogView
tests/Mcacc.Core.Tests/   xUnit v3, reine Logik + Fixtures (echte WMI-/ps-Snapshots anonymisiert)
tests/Mcacc.App.Tests/    ViewModel-Flows mit Fakes + Avalonia.Headless-Smoke
scripts/publish-windows.ps1 · scripts/build-macos.sh · build/macos/Info.plist.template
docs/superpowers/specs/2026-09-23-mcacc-design.md · docs/spike-results.md · README.md
```

## Kernabläufe

### Claude Desktop (Account A)

1. **Installation finden.** Wird keine gefunden → Hinweis.
2. **Laufende Instanzen erkennen:**
   - Windows per WMI: `SELECT ProcessId,ParentProcessId,ExecutablePath,CommandLine,CreationDate FROM Win32_Process WHERE Name='claude.exe'`.
   - macOS per `ps -axww -o pid=,ppid=,lstart=,command=`.
   - Den Account je Instanz über den Befund 3 zuordnen.
3. **Konflikt** (anderer Account läuft) → Dialog „Claude läuft mit ‚X‘. Beenden und mit ‚A‘ neu starten? Ungespeicherte Eingaben gehen verloren.“
4. **Beenden:**
   - Startzeit prüfen, damit keine wiederverwendete PID getroffen wird.
   - Windows: `CloseMainWindow`, 4 s warten, dann `Kill(entireProcessTree)`.
   - macOS: SIGTERM, bis 8 s warten, dann SIGKILL.
   - Danach erneut erkennen, bis alles weg ist.
5. **Starten:**
   - Windows: `ProcessStartInfo` auf den Alias, `UseShellExecute=false`, `ArgumentList=["--user-data-dir=<abs>/desktop"]`, Env `CLAUDE_CONFIG_DIR=<abs>/claude`. Für Standard ohne Argument und mit **entfernter** Variable. Die Scrub-Liste wird immer entfernt.
   - macOS: `/usr/bin/open -n -a <Claude.app> --env CLAUDE_CONFIG_DIR=… --args --user-data-dir=…`. Für Standard nur `open -n -a <Claude.app>`, denn `open` vererbt keine Env.
6. **Aufräumen:** `lastAccountId`/`lastTarget` speichern. Bei `closeAfterLaunch` den Switcher etwa 400 ms nach erfolgreichem Start beenden.

### Claude Code (Account A)

Die Variable wird immer **innerhalb** des Shell-Kommandos gesetzt, nicht vererbt.

| Terminal | Aufruf |
|---|---|
| Windows Terminal + PowerShell (auto: pwsh, sonst powershell) | `wt.exe -w new new-tab --title "Claude · A" --suppressApplicationTitle -d "<home>" powershell.exe -NoLogo -NoExit -Command "$env:CLAUDE_CONFIG_DIR='…'\; claude"` (`;` wird für wt als `\;` escaped) |
| Windows Terminal + cmd / Eingabeaufforderung | Die Batch-Datei `launch/<slug>.cmd` setzt die Variable, macht `cd /d %USERPROFILE%`, ruft `claude` auf und endet mit `@echo on`. Aufruf: `cmd /k <slug>.cmd` mit dem Arbeitsordner `launch/` |
| PowerShell 7 / Windows PowerShell direkt | Die exe mit `-NoLogo -NoExit -Command "<script>"` und `WorkingDirectory=home`. Das Script setzt auch den Fenstertitel |
| macOS Terminal / iTerm2 / Ghostty | Das Skript `launch/<slug>.command` (0700, LF): `export`/`unset CLAUDE_CONFIG_DIR`, `unset CLAUDECODE`, `cd "$HOME"`, `"${SHELL:-/bin/zsh}" -l -i -c 'claude'` und danach `exec "$SHELL" -l -i`. Aufruf: `open -a Terminal\|iTerm <datei>` bzw. `open -na Ghostty --args -e <datei>`. Es wird keine Automation-Berechtigung benötigt |

- **Standard-Account:** Die Variable wird entfernt, in PowerShell mit `Remove-Item Env:CLAUDE_CONFIG_DIR -ErrorAction Ignore`.
- **Warum `-Command` statt `-EncodedCommand` (primär):**
  - Die Execution Policy (Firmen-GPO) gilt für `-Command` nicht.
  - EDR-Tools stufen `-Command` als weniger verdächtig ein.
  - `-EncodedCommand` bleibt als Fallback im Builder.
- **Terminal-Erkennung:**
  - Windows: `wt.exe`, `pwsh.exe` (PATH, ProgramFiles), `powershell.exe`, `%ComSpec%`.
  - macOS: Terminal immer. iTerm2 und Ghostty werden in `/Applications`, `~/Applications` und per `mdfind` gesucht.
- **`claude` nicht gefunden:** Es gibt nur einen Hinweis, der Start wird nicht blockiert, weil die Login-Shell `claude` vielleicht trotzdem findet.

### Nutzungslimits

- **Abruf:** beim Start parallel für alle Accounts und über den Aktualisieren-Button, je Account höchstens alle 60 s.
- **Token:**
  - Das Token wird nur gelesen.
  - Ist es abgelaufen, geht kein Request raus, und die Kachel zeigt „Abgelaufen – Claude Code starten zum Aktualisieren“.
  - Bei 401, 429 oder einem Netzfehler zeigt die Kachel den Cache-Wert mit „Stand HH:mm“.
  - Ohne Login steht dort „Noch nicht angemeldet“.
- **Anzeige:**
  - Zwei schmale Balken, „5h“ und „Woche“, mit Prozent und Reset-Zeit.
  - Ab 90 % in Warnfarbe.
  - `null`-Werte (z. B. Enterprise) werden als „—“ angezeigt.
- **Mac:** Der erste Keychain-Zugriff zeigt einen Freigabedialog. Die README erklärt ihn.

### Accounts anlegen, bearbeiten und entfernen

- **Editor:** Name (Pflicht, eindeutig, ≤ 40 Zeichen) und eine von 8 Farb-Swatches.
- **Nur beim Anlegen:** „Einstellungen übernehmen von“ mit Checkboxen. Nicht vorhandene Elemente sind deaktiviert.
- **Pfade anpassen:** Eine Unteroption für settings.json schreibt Verweise auf den Quellordner um, in den Formen `C:\…\.claude`, `C:/…`, `~/.claude` und `$HOME/.claude`.
- **Niemals kopiert** werden `.credentials.json`, `.claude.json`, history, projects, sessions und plugins. Das garantiert die Whitelist.
- **Entfernen:**
  - Der Dialog hat die Checkbox „Daten auch löschen“ und listet die Ordner auf. Bei gesetzter Checkbox wird der Button rot.
  - Blockiert, solange Desktop mit diesem Account läuft.
  - Der Default-Account kann nicht entfernt werden.

## UI, Theme und Bedienung

**Fenster und Navigation**
- Fenster 760×540 (min. 560×440), zentriert, Standard-Fensterrahmen.
- Seitenwechsel über `TransitioningContentControl` mit CrossFade, 150 ms.
- Dialoge erscheinen als Overlay mit abgedunkeltem Hintergrund.

**Account-Auswahl**
- Serif-Headline „Mit welchem Account?“.
- Kacheln mit Initialen-Avatar in der Account-Farbe, Name, E-Mail, dem Badge „Läuft“ und den UsageBars.
- Ein „⋮“-Menü bzw. Rechtsklick bietet Bearbeiten, Entfernen und Ordner öffnen.
- Eine gestrichelte Kachel „Account hinzufügen“.
- Zahnrad für die Einstellungen und ein Aktualisieren-Icon.

**Zielauswahl**
- Zurück-Pfeil und Chip mit dem Account.
- Kachel Claude Desktop (Icon `desktop_windows`). Der Untertitel zeigt den Zustand, z. B. „Läuft bereits“ oder „Läuft mit ‚Firma‘ – wird beendet“.
- Kachel Claude Code (Icon `terminal`), Untertitel „im Windows Terminal“ bzw. das gewählte Terminal.

**Einstellungen**
- Terminal-Auswahl (nur installierte). Bei Windows Terminal zusätzlich die Shell.
- „Nach dem Start schließen“.
- Datenordner mit „Im Explorer/Finder öffnen“.
- Version.

**Tastatur**
- Pfeiltasten (XYFocus), Enter, 1–9 für die direkte Auswahl, Esc/Backspace für zurück.
- D für Desktop, C für Code.
- Strg/Cmd+N für einen neuen Account, Strg/Cmd+, für die Einstellungen.
- Der zuletzt genutzte Account und das zuletzt genutzte Ziel haben den Fokus vorab, sodass zweimal Enter reicht.

**Tokens**

| | Hell | Dunkel |
|---|---|---|
| Bg | `#FAF9F5` | `#262624` |
| Surface / Hover | `#FFFFFF` / `#F5F4EE` | `#30302E` / `#3A3A37` |
| Border / Strong | `#E8E6DC` / `#D1CFC5` | `#3E3E38` / `#4A4945` |
| Text / Sekundär / Tertiär | `#141413` / `#5E5D59` / `#87867F` | `#FAF9F5` / `#C2C0B6` / `#9C9A92` |
| Akzent (Icons, Fokusring, Balken) | `#D97757` | `#D97757` |
| AccentFill (Buttons) / AccentSubtle | `#C96442` / `#F5E1D8` | `#D97757` / `#4A3129` |
| Danger | `#B53333` | `#E5716F` |

- **Fluent:** Der Accent der Fluent-Palette ist `#D97757`.
- **Schriften:** Überschriften in Source Serif 4, die UI in Inter.
- **Kachel:**
  - Radius 14, 1 px Rand.
  - Hover: orangener Rand und 2 px Anhebung.
  - Pressed: Skalierung 0,98.
  - Fokus: 2 px Akzent-Ring.
  - `AutomationProperties.Name` ist gesetzt.

## Umsetzungsschritte

0. **Setup:** `git init` und `.gitignore`. Kanban-Ticket anlegen (Skill `kanban`). Die Spec nach `docs/superpowers/specs/2026-09-23-mcacc-design.md` schreiben und committen. Scaffold anlegen (slnx, Props, 3 src- und 2 test-Projekte), bis `dotnet build` und `dotnet test` grün sind. **Ab hier in einer eigenständigen `claude`-Session im Windows Terminal arbeiten**, nicht im Desktop-Code-Tab (Befund 5).
1. **Spike, Benjamin führt aus:** Ich liefere `docs/spike-checklist.md` mit Befehlen. Die Ergebnisse kommen nach `docs/spike-results.md`, die Prozess-Snapshots als Test-Fixtures.
   - **Windows:**
     - W1: `--user-data-dir` über den Alias wird wirklich genutzt (auch mit Leerzeichen/ä im Pfad), und `%APPDATA%\Claude` bleibt unverändert.
     - W2: Wo steht das Flag im WMI-Snapshot?
     - W3: Erreicht `CLAUDE_CONFIG_DIR` den Code-Tab?
     - W4: Parallelstart, und fokussiert ein zweiter Start desselben Profils nur?
     - W5: Beendet `CloseMainWindow` die App oder landet sie nur im Tray? Ist der Zustand nach dem Kill intakt?
     - W6: Login-Deeplink mit Google/SSO und per E-Mail, mit und ohne laufende Standard-Instanz.
     - W7: wt-Befehl mit `\;`, `-d` mit Leerzeichen und die cmd-Variante.
     - W8: `/login` mit Config-Dir schreibt nur in den Dir.
     - W9: Usage-Endpoint für beide Accounts einmal aufrufen und das Format festhalten.
   - **macOS:**
     - M1: Bundle-ID und `open --env`.
     - M2/M3: Profil und Env.
     - M4: `ps`-Erkennung.
     - M5: SIGTERM.
     - M6: Login.
     - M7: Keychain-Servicename je Config-Dir.
     - M8: `.command` in Terminal, iTerm2 und Ghostty.
     - M10: Die ad-hoc-signierte `.app` startet aus dem Finder.
2. **Core-Basis (TDD):** Model, Paths, ConfigStore/Validator, SlugGenerator, AccountService, ConfigCopy, Deleter-Guard.
3. **Quoting und alle Builder/Planner (TDD):** für beide PathStyles.
4. **Erkennung (TDD):** CommandLineSwitch, PsOutputParser, Classifier und Matcher gegen die echten Fixtures.
5. **Services (TDD mit Fakes):** DesktopLaunchService, CodeLaunchService, ClaudeJsonLoginReader, Credentials- und Usage-Parser, UsageService.
6. **Platform:** zuerst Windows, dann macOS: Runner, Snapshot, Locator, Terminator, Kataloge, Credential-Quellen, HTTP und `--diagnose`.
7. **App-Shell:** Tokens, Fonts, MIcon, MainWindow mit Overlay-Dialogen, Navigation.
8. **Seiten und Dialoge:** Tastatur, Texte, UsageBar, „Läuft“-Badge.
9. **Flows verdrahten:** Launch, Close-after-launch, Fehlerbanner, Logging.
10. **Packaging und README** (Bedienung, Einschränkungen: Login-Deeplink und inoffizieller Endpoint).
    - `publish-windows.ps1`: Tests, dann `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true` und ein ZIP.
    - `build-macos.sh`:
      - publish für osx-arm64, optional x64 oder universal per `lipo`
      - `.app`-Bundle zusammenbauen, Info.plist aus dem Template, Icon per `sips`/`iconutil`
      - `xattr -cr`, dann `codesign --force --deep --sign -`, dann `codesign --verify`, dann ZIP per `ditto`
11. **Review:** Skills `superpowers:requesting-code-review` und `verification-before-completion` anwenden, E2E auf beiden OS durchgehen, das Ticket abschließen.

## Verifikation

**Unit-Tests** (`dotnet test MCACC.slnx`):
- **Slugs:** Umlaute („Kunde Müller & Söhne“ → `kunde-mueller-soehne`), `con`, `default`, Kollision, leerer Name.
- **ConfigStore:** Roundtrip, `.bak`, kaputte Datei, neuere Version, unbekannte Felder, Validator.
- **Pfade:** Resolver für Win und Posix.
- **`.claude.json`-Reader:** Case-Duplikat-Keys wie „…/Azura“ und „…/AZURA“, abgeschnittenes JSON, gesperrte Datei.
- **`CommandLineSwitch` und Classifier:** gegen echte Snapshots (Flag nur am Kind, CLI-Prozesse ausgeschlossen, zwei Instanzen, Squirrel).
- **Planner:**
  - Standard entfernt die Variable.
  - Das Profil übergibt `--user-data-dir` als *ein* Argument.
  - Die macOS-`open`-Argumentlisten.
- **Quoting:** `'`, `’`, `%`, `;`, `$`, Leerzeichen und Umlaut im Pfad. Die Batch-Datei hat CRLF und `@echo on`, `.command` hat LF und Shebang.
- **LaunchService mit Fakes:** nicht installiert, gleicher Account, Konflikt mit Bestätigung (nur die konfliktierende Instanz wird beendet), Quit-Fehler (kein Start).
- **Usage-Parser/Service:** `null`-Felder, abgelaufenes Token (kein Request), 401 und 429 → Cache, Mindestabstand.
- **Copier:** nur die Whitelist, keine Credentials. Der Rewriter lässt `.claude.json` in Ruhe. Der Deleter-Guard lehnt Default, `..` und den Root ab.

**App-Tests:**
- **ViewModel-Flows:**
  - Auswahl führt zur Zielseite.
  - Konflikt zeigt den Dialog. Bestätigen startet und schließt; Abbrechen tut nichts.
  - Editor-Validierung.
  - Einstellungen werden persistiert.
- **Headless:** Zwei Ziel-Kacheln werden gerendert, die Light- und Dark-Tokens werden aufgelöst.

**Manuelles E2E**, zuerst Windows aus einer eigenständigen Session, danach Mac:
1. Erststart: Die Kachel „Standard“ zeigt die Firmen-E-Mail und die Limits.
2. „Privat“ anlegen, mit Kopie von CLAUDE.md, settings und skills. Plugins werden nicht kopiert.
3. Claude Code → Privat in jedem installierten Terminal:
   - `/login` klappt, die Credentials liegen in `~/.mcacc/profiles/privat/claude`.
   - `/status` zeigt den Privat-Account.
   - Nach `/exit` bleibt die Shell offen.
4. Desktop → Privat, während Standard läuft:
   - Der Dialog erscheint, Standard wird beendet, Privat startet mit Login-Screen.
   - Der Login landet im richtigen Profil.
   - Der Code-Tab nutzt die Privat-Config.
5. Zurück zu Standard: ohne neuen Login eingeloggt.
6. Die Limits beider Accounts sind sichtbar. Bei abgelaufenem Token erscheint der Hinweistext.
7. MCACC mit gesetztem `CLAUDE_CONFIG_DIR` starten: Die Variable wird nicht an Standard durchgereicht.
8. Entfernen ohne und mit Daten. Bei laufender Instanz ist das Löschen blockiert.
9. Hell/Dunkel live umschalten, nur mit der Tastatur bedienen, 150 % Skalierung, `MCACC_HOME` mit Leerzeichen, Umlaut und Apostroph.

## Risiken und Fallbacks

1. **Windows ignoriert `--user-data-dir`** (die App setzt userData intern um). Fallback „Swap-Modus“: Eine Junction ersetzt `%APPDATA%\Claude` und wird nur bei gestoppter App umgehängt. Das braucht einen eigenen Spike.
2. **Env erreicht die MSIX-App nicht.** Dann ist der Code-Tab nicht koppelbar; das wird als Einschränkung dokumentiert.
3. **Login-Deeplink landet im falschen Profil** (Befund 4). Die Reihenfolge der Ausweichlösungen:
   - E-Mail-Login in der App.
   - Einloggen, während keine andere Instanz läuft.
   - Optional den HKCU-Handler `claude://` beim Start eines Zusatzprofils temporär auf Alias + Flag umbiegen. Das kommt nur nach positivem Spike W6 und nur nach Rückfrage bei Benjamin.
4. **Inoffizieller Usage-Endpoint** ändert sich oder drosselt. Dann gibt es Cache-Anzeige und sauberes Degradieren, keine Abstürze.
5. **Hartes Beenden** kann ungespeicherte Eingaben kosten. Der Dialog warnt, und zuerst wird sanft beendet.
6. **Claude-Updates** ändern Pfade oder Flags. Die Regeln liegen zentral in DesktopProcessRules/Locator; `--diagnose` und das Log helfen bei der Fehlersuche.
7. **Firmen-EDR/AppLocker** reagiert auf PowerShell. `-Command` und die Batch-Variante sind die unauffälligsten Wege.
8. **Plattenplatz:** Jedes Desktop-Profil lädt eigene claude-code-Binaries und VM-Bundles (mehrere 100 MB). Das steht in der README.
9. **Verteilung:** Auf dem Mac gibt es Gatekeeper-Warnungen bei Ad-hoc-Signatur, unter Windows SmartScreen bei unsignierter exe.
