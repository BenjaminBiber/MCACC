# MCACC Spike-Checkliste (Windows & macOS)

Diese Checkliste führst du **manuell** aus (Windows-Rechner und Mac). Die Ergebnisse trägst du in
`docs/spike-results.md` ein; sie entscheiden Detailfragen für die späteren Implementierungs-Tasks.

> ⚠️ **Wichtig:** Führe alle Befehle in einem normalen Terminal aus (Windows Terminal bzw.
> Terminal.app/iTerm auf dem Mac) — **niemals** im "Code"-Tab von Claude Desktop selbst. Der
> Code-Tab läuft im MSIX-Container mit virtualisiertem Dateisystem, Pfade stimmen dort nicht mit
> der echten Dateisystemsicht überein. Außerdem beendet das Schließen von Claude Desktop sofort
> jede Code-Tab-Session, die darin läuft.

## Bekannte Fakten (Ausgangslage)

- Claude Desktop läuft auf diesem Windows-Rechner als MSIX-Paket:
  `C:\Program Files\WindowsApps\Claude_<ver>_x64__pzs8sxrjxfjjc\app\Claude.exe`.
- Alias zum Starten aus einer Shell: `%LOCALAPPDATA%\Microsoft\WindowsApps\claude-desktop.exe`.
- Squirrel-Fallback (ältere Installationsart, hier nicht aktiv): `%LOCALAPPDATA%\AnthropicClaude\claude.exe`.
- MSIX Package Family Name: `Claude_pzs8sxrjxfjjc`.
- Der `claude://`-Protocol-Handler ist registriert unter
  `HKCU\Software\Classes\claude\shell\open\command`.
- Aus einer früheren Beobachtung: Der **Hauptprozess** von Claude Desktop läuft **ohne**
  `--user-data-dir` in seiner Kommandozeile — das Flag taucht nur bei **Kindprozessen** auf.
  W2 verifiziert das systematisch.
- macOS-Bundle vermutlich `/Applications/Claude.app` mit Bundle-ID `com.anthropic.claudefordesktop`
  — wird in M1 bestätigt.

## Variablen setzen

Windows (PowerShell):
```powershell
$spike = "$env:USERPROFILE\.mcacc\spike"
```

macOS (zsh):
```zsh
spike=~/.mcacc/spike
```

Der Desktop-Profilordner heißt bewusst `desk top ä` (Leerzeichen + Umlaut), um Pfad-Escaping in
allen Befehlen mit zu testen.

Hilfsbefehl zum Anonymisieren von Text-Fixtures (Benutzername durch `USER` ersetzen), z. B. nach
W2/M4, bevor eine Datei committet wird:

```powershell
$datei = "<Pfad zur Datei>"
(Get-Content $datei) -replace [Regex]::Escape($env:USERNAME), 'USER' | Set-Content $datei
```

```zsh
datei="<Pfad zur Datei>"
sed -i '' "s/$USER/USER/g" "$datei"
```

---

## Windows

### W1 — `--user-data-dir` wird honoriert

**Ziel:** Prüfen, dass das Flag ein eigenständiges Profil anlegt und die Standard-Datenordner
unangetastet lässt.

Claude zuerst vollständig beenden (Tray-Icon → Beenden). Dann Zeitstempel **vorher** notieren:
```powershell
(Get-Item "$env:APPDATA\Claude").LastWriteTime
(Get-Item "$env:LOCALAPPDATA\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude").LastWriteTime
```

Mit eigenem Profil starten:
```powershell
& "$env:LOCALAPPDATA\Microsoft\WindowsApps\claude-desktop.exe" "--user-data-dir=$spike\desk top ä"
```

Nach dem Start: Zeitstempel erneut prüfen (müssen unverändert sein) und Inhalt des neuen Ordners
ansehen:
```powershell
(Get-Item "$env:APPDATA\Claude").LastWriteTime
(Get-Item "$env:LOCALAPPDATA\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude").LastWriteTime
Get-ChildItem "$spike\desk top ä"
```

**Erwartung:** Frischer Login-Bildschirm; Dateien erscheinen unter `$spike\desk top ä`; beide
Zeitstempel oben bleiben unverändert.

**Notieren:** Erwartung erfüllt ja/nein, welche der beiden Standardordner überhaupt existieren.

### W1b — Speicherort des Default-Profils

**Ziel:** Feststellen, wo die Dateien des Default-Accounts physisch liegen (aus einer normalen
Shell, nicht aus dem Code-Tab).

```powershell
Get-Item "$env:APPDATA\Claude"
Get-Item "$env:LOCALAPPDATA\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude"
Get-ChildItem "$env:LOCALAPPDATA\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude" -ErrorAction SilentlyContinue
```

**Erwartung:** Mindestens einer der beiden Pfade existiert und enthält die eigentlichen
Profildaten (MSIX-Apps spiegeln `%APPDATA%` oft nach `LocalCache\Roaming`).

**Notieren:** Welcher Pfad existiert, welcher enthält die Nutzdaten, Ordnergröße/Inhalt grob.

### W2 — Prozess-Snapshot

**Ziel:** Zeigen, ob `--user-data-dir` nur bei Kindprozessen auftaucht.

Für jedes der drei Szenarien (nur Default, nur Spike, beide gleichzeitig) einmal ausführen —
`<scenario>` durch `default` / `spike` / `both` ersetzen:
```powershell
Get-CimInstance Win32_Process -Filter "Name='claude.exe'" | Select-Object ProcessId,ParentProcessId,ExecutablePath,CommandLine,CreationDate | Export-Csv "$spike\wmi-<scenario>.csv" -NoTypeInformation -Encoding utf8
```

**Erwartung:** Der Hauptprozess (kein `--type=` in der CommandLine) trägt **kein**
`--user-data-dir`; nur Kindprozesse (`--type=renderer`, `--type=gpu-process`, …) tragen es.

**Notieren:** Pro Szenario die Prozess-/Eltern-Kind-Struktur, ob die Erwartung zutrifft.
Vor dem Commit als Fixture in `tests/Mcacc.Core.Tests/Fixtures/`: Benutzername in jeder CSV mit
dem Hilfsbefehl oben durch `USER` ersetzen.

### W3 — Env-Variable erreicht die App

**Ziel:** Prüfen, ob `CLAUDE_CONFIG_DIR` bis in die Code-Tab-Session durchgereicht wird.

```powershell
$env:CLAUDE_CONFIG_DIR = "$spike\claude"
& "$env:LOCALAPPDATA\Microsoft\WindowsApps\claude-desktop.exe" "--user-data-dir=$spike\desk top ä"
```

Im Code-Tab dieser Instanz eine Session starten. Danach prüfen, ob Dateien angelegt wurden:
```powershell
Get-ChildItem "$spike\claude\projects"
```

Alternativ in der Session selbst nachfragen, was `$env:CLAUDE_CONFIG_DIR` dort zeigt.

Variable danach wieder entfernen:
```powershell
Remove-Item Env:CLAUDE_CONFIG_DIR
```

**Erwartung:** `$spike\claude\projects` enthält Dateien; die Env-Variable kommt im Kindprozess an.

**Notieren:** Ob Dateien erschienen sind, welcher Account im Code-Tab verwendet wird
(Desktop-Login vs. eigene CLI-Anmeldung).

### W4 — Parallelbetrieb und Zweitstart

**Ziel:** Klären, ob Default und Spike-Profil gleichzeitig laufen können und was ein Zweitstart
des Spike-Profils macht.

```powershell
& "$env:LOCALAPPDATA\Microsoft\WindowsApps\claude-desktop.exe"
```
```powershell
& "$env:LOCALAPPDATA\Microsoft\WindowsApps\claude-desktop.exe" "--user-data-dir=$spike\desk top ä"
```

Danach den zweiten Befehl (Spike-Profil) erneut ausführen.

**Erwartung:** Beide Instanzen laufen parallel in getrennten Fenstern; der zweite Start des
Spike-Profils fokussiert nur das bestehende Fenster statt eine neue Instanz zu öffnen.

**Notieren:** Ja/Nein für beide Verhalten.

### W5 — Beenden

**Ziel:** Unterschied zwischen "Fenster schließen" und echtem Prozessende klären.

PID ermitteln:
```powershell
Get-Process claude | Select-Object Id,MainWindowTitle,Path,StartTime
```

Fenster schließen (`<mainPid>` einsetzen):
```powershell
(Get-Process -Id <mainPid>).CloseMainWindow()
```
```powershell
Get-Process -Id <mainPid> -ErrorAction SilentlyContinue
```

Falls der Prozess noch läuft (nur ins Tray minimiert), hart beenden:
```powershell
Stop-Process -Id <mainPid> -Force
Get-Process claude -ErrorAction SilentlyContinue | Stop-Process -Force
```

Anschließend neu starten und prüfen, ob Login/Chats erhalten sind.

> ⚠️ Beenden von Claude Desktop beendet auch jede Code-Tab-Session, die gerade darin läuft.

**Erwartung:** `CloseMainWindow()` minimiert vermutlich nur ins Tray statt den Prozess zu beenden;
`Stop-Process` beendet zuverlässig; nach Neustart sind Login und Chatverlauf intakt.

**Notieren:** Tatsächliches Verhalten von `CloseMainWindow()`, ob nach `Stop-Process` + Neustart
alles erhalten bleibt.

### W6 — Login-Deep-Link

**Ziel:** Klären, wo ein Login (Google/SSO bzw. E-Mail-Code) landet, und wie der
Protocol-Handler aussieht.

Login testen: einmal in das Spike-Profil einloggen, während die Default-Instanz läuft, einmal
während sie nicht läuft (beide Login-Wege: SSO und E-Mail-Code).

Protocol-Handler anzeigen:
```powershell
reg query "HKCU\Software\Classes\claude\shell\open\command"
```

**Optionaler Test — nur mit Backup, danach zwingend zurücksetzen:**

Backup zuerst:
```powershell
reg export "HKCU\Software\Classes\claude" "$spike\claude-protocol-backup.reg"
```

Handler auf das Spike-Profil umbiegen (PS-nativ statt `reg add`, damit PowerShell-5.1-eigene
Quoting-Bugs bei nativen Aufrufen nicht zuschlagen):
```powershell
$alias = "$env:LOCALAPPDATA\Microsoft\WindowsApps\claude-desktop.exe"
$newCmd = '"' + $alias + '" "--user-data-dir=' + "$spike\desk top ä" + '" "%1"'
Set-ItemProperty -LiteralPath 'HKCU:\Software\Classes\claude\shell\open\command' -Name '(default)' -Value $newCmd
```

Direkt zurücklesen, um die geschriebene Kommandozeile zu verifizieren:
```powershell
(Get-ItemProperty -LiteralPath 'HKCU:\Software\Classes\claude\shell\open\command').'(default)'
```

Login erneut versuchen, danach prüfen, ob Claude den Schlüssel beim Start selbst überschrieben
hat:
```powershell
reg query "HKCU\Software\Classes\claude\shell\open\command"
```

Zurücksetzen (immer ausführen, auch wenn der Test fehlschlägt):
```powershell
reg import "$spike\claude-protocol-backup.reg"
```

**Erwartung:** Login landet nachvollziehbar in einem der beiden Profile; der Registry-Schlüssel
zeigt normalerweise auf den MSIX-Launcher, nicht direkt auf `claude-desktop.exe`.

**Notieren:** Wo der Login jeweils landet (mit/ohne laufende Default-Instanz, SSO/E-Mail-Code),
Inhalt des Original-Schlüssels, ob Claude ihn beim Start zurückschreibt.

### W7 — Windows Terminal

**Ziel:** Einen neuen WT-Tab mit gesetzter `CLAUDE_CONFIG_DIR` öffnen (bei bereits offenem
WT-Fenster).

```powershell
wt.exe -w new new-tab --title "Claude Code · Spike" --suppressApplicationTitle --tabColor "#6A9BCC" -d "$env:USERPROFILE" powershell.exe -NoLogo -NoExit -Command "`$env:CLAUDE_CONFIG_DIR='$spike\claude'\; claude"
```

Erläuterung der Escaping-Tricks (geprüft per `Write-Output`, ohne `wt`/`claude` zu starten): das
Backtick vor `$env` verhindert, dass die äußere PowerShell die Variable selbst auflöst — der Text
`$env:CLAUDE_CONFIG_DIR` bleibt wörtlich erhalten und wird erst in der neuen Shell ausgewertet.
`$spike` dagegen **wird** von der äußeren PowerShell expandiert. `\;` ist ein von `wt.exe` maskiertes
Semikolon, damit `wt` es nicht als eigenen Kommando-Trenner interpretiert, sondern als normales
Semikolon an `powershell.exe -Command` weiterreicht. Der so aufgelöste Text, den die neue Shell
erhält, lautet (Beispielwert für `$spike`):
```
$env:CLAUDE_CONFIG_DIR='C:\Users\<Benutzername>\.mcacc\spike\claude'; claude
```

**Erwartung (wt.exe-Variante):** Neues Fenster öffnet sich, `$env:CLAUDE_CONFIG_DIR` ist in der
neuen Shell gesetzt, `claude` startet, die Shell bleibt nach `/exit` offen.

**cmd-Variante:** `spike.cmd` erzeugen (CRLF, UTF-8 ohne BOM):
```powershell
$content = @(
  '@chcp 65001 >nul',
  '@echo off',
  'set "CLAUDECODE="',
  'set "CLAUDE_CODE_ENTRYPOINT="',
  'set "CLAUDE_CODE_SSE_PORT="',
  'set "CLAUDE_CONFIG_DIR=%USERPROFILE%\.mcacc\spike\claude"',
  'cd /d "%USERPROFILE%"',
  'title Claude Code - Spike',
  'claude',
  '@echo on'
) -join "`r`n"
$content += "`r`n"
[IO.File]::WriteAllText("$spike\spike.cmd", $content, (New-Object Text.UTF8Encoding $false))
```

Inhalt von `spike.cmd` zur Referenz:
```
@chcp 65001 >nul
@echo off
set "CLAUDECODE="
set "CLAUDE_CODE_ENTRYPOINT="
set "CLAUDE_CODE_SSE_PORT="
set "CLAUDE_CONFIG_DIR=%USERPROFILE%\.mcacc\spike\claude"
cd /d "%USERPROFILE%"
title Claude Code - Spike
claude
@echo on
```

Damit starten:
```powershell
wt.exe -w new new-tab -d "$spike" cmd.exe /k spike.cmd
```

**Erwartung:** Beide Varianten öffnen einen Tab mit gesetzter Variable, `claude` startet, die
Shell bleibt danach offen.

**`%VAR%`-Expansion:** Prüfen, ob Windows Terminal `%…%` im `-d`-Ordner oder in der Kommandozeile
selbst expandiert. MCACC übergibt Pfade mit `%` unverändert an `wt` (nur `;` wird als `\;`
maskiert).
```powershell
$pct = "$spike\pct %USERNAME% test"
New-Item -ItemType Directory -Force -Path $pct | Out-Null
wt.exe -w new new-tab -d "$pct" powershell.exe -NoLogo -NoExit -Command "(Get-Location).Path\; Write-Output '%USERNAME%'"
```

**Erwartung:** Der neue Tab startet im Ordner `…\pct %USERNAME% test` und gibt diesen Pfad sowie
`%USERNAME%` wörtlich aus. Steht dort stattdessen der echte Benutzername, oder meldet `wt`, dass
der Ordner nicht existiert, dann expandiert Windows Terminal die Variable.

**Notieren:** Ob beide Varianten wie erwartet funktionieren, insbesondere ob die
Escaping-Konstruktion aus der ersten Variante tatsächlich `claude` startet statt einen Fehler zu
werfen. Außerdem, ob `%USERNAME%` im `-d`-Ordner und im `-Command`-Skript expandiert wurde
(angezeigter Pfad und Ausgabe).

### W8 — CLI-Konfigurationsordner

**Ziel:** Prüfen, dass `/login` im W7-Fenster nur `$spike\claude` verändert.

Hash vorher:
```powershell
Get-FileHash "$env:USERPROFILE\.claude.json" -Algorithm SHA256 -ErrorAction SilentlyContinue
```

Im W7-Fenster (mit gesetzter `CLAUDE_CONFIG_DIR`) `/login` mit dem privaten Account ausführen.
Danach:
```powershell
Get-FileHash "$env:USERPROFILE\.claude.json" -Algorithm SHA256 -ErrorAction SilentlyContinue
Get-ChildItem "$spike\claude"
Test-Path "$spike\claude\.credentials.json"
Test-Path "$spike\claude\.claude.json"
```

**Erwartung:** Hash von `~\.claude.json` unverändert; Credentials und `.claude.json` liegen unter
`$spike\claude`.

**Notieren:** Hash-Vergleich, welche Dateien tatsächlich unter `$spike\claude` liegen.

### W9 — Usage-Endpoint

**Ziel:** Token nie ausgeben, nur die JSON-Antwort des Endpoints speichern.

Für den Default-Account:
```powershell
$json = Get-Content "$env:USERPROFILE\.claude\.credentials.json" -Raw | ConvertFrom-Json
$t = $json.claudeAiOauth.accessToken
Invoke-RestMethod https://api.anthropic.com/api/oauth/usage -Headers @{ Authorization = "Bearer $t"; 'anthropic-beta' = 'oauth-2025-04-20' } | ConvertTo-Json -Depth 6 | Set-Content "$spike\usage-default.json"
Remove-Variable t
```

Für das Spike-Profil:
```powershell
$json = Get-Content "$spike\claude\.credentials.json" -Raw | ConvertFrom-Json
$t = $json.claudeAiOauth.accessToken
Invoke-RestMethod https://api.anthropic.com/api/oauth/usage -Headers @{ Authorization = "Bearer $t"; 'anthropic-beta' = 'oauth-2025-04-20' } | ConvertTo-Json -Depth 6 | Set-Content "$spike\usage-spike.json"
Remove-Variable t
```

> ⚠️ `$t` niemals mit `Write-Output`/`Write-Host` ausgeben und nicht in eine Datei schreiben.
> Nur die JSON-Antwort des Endpoints wird gespeichert.

**Erwartung:** Beide Aufrufe liefern gültiges JSON mit Nutzungsdaten.

**Notieren:** Struktur der Antwort. Vor dem Commit als Fixture (`usage-*.json` unter
`tests/Mcacc.Core.Tests/Fixtures/`): Name/E-Mail-Adresse im JSON manuell durch Platzhalter
ersetzen (z. B. `USER`, `user@example.com`).

---

## macOS

### M1 — Bundle-ID und `open`-Fähigkeiten

**Ziel:** Bundle-ID bestätigen und prüfen, ob `open --env` verfügbar ist.

```zsh
osascript -e 'id of app "Claude"'
```
```zsh
open --help 2>&1 | grep -e '--env'
```

**Erwartung:** Bundle-ID `com.anthropic.claudefordesktop`; `--env` ist ab neueren macOS-Versionen
vorhanden (Grundlage für D6).

**Notieren:** Tatsächliche Bundle-ID, ob `--env` in der Hilfe auftaucht, macOS-Version.

### M2 / M3 — Eigenes Profil starten

**Ziel:** Frisches Profil per `open --env` erzeugen und prüfen, dass Standarddaten unangetastet
bleiben.

```zsh
open -n -a /Applications/Claude.app --env CLAUDE_CONFIG_DIR="$spike/claude" --args --user-data-dir="$spike/desk top ä"
```

Vorher/nachher vergleichen:
```zsh
stat -f '%m %N' ~/Library/Application\ Support/Claude
```

PID ermitteln und Env-Variable im Prozess prüfen:
```zsh
ps -axo pid,ppid,command | grep -i "[C]laude.app/Contents/MacOS/Claude"
```
```zsh
ps eww <pid> | tr ' ' '\n' | grep CLAUDE_CONFIG_DIR
```

Im Code-Tab dieser Instanz eine Session starten, danach prüfen:
```zsh
ls "$spike/claude"
```

**Erwartung:** `~/Library/Application Support/Claude` unverändert; `CLAUDE_CONFIG_DIR` ist im
Prozess-Environment sichtbar; Code-Tab schreibt nach `$spike/claude`.

**Notieren:** Zeitstempel-Vergleich, ob die Variable im Prozess ankommt, ob Dateien erscheinen.

### M4 — Prozess-Snapshot

**Ziel:** Wie W2, für macOS.

```zsh
ps -axww -o pid=,ppid=,lstart=,command= | grep -i claude > "$spike/ps-<scenario>.txt"
```

Für die Szenarien `default`, `spike`, `both` wiederholen. Vor dem Commit als Fixture in
`tests/Mcacc.Core.Tests/Fixtures/`: Benutzername mit dem Hilfsbefehl oben (`sed`) durch `USER`
ersetzen.

**Erwartung:** Wie W2 — Klärung, ob und wo `--user-data-dir` in der Kommandozeile auftaucht.

**Notieren:** Pro Szenario die Prozessstruktur.

### M5 — Beenden

**Ziel:** Exit-Zeit messen und prüfen, ob der Zustand nach Neustart erhalten bleibt.

```zsh
kill -TERM <mainPid>
SECONDS=0
while kill -0 <mainPid> 2>/dev/null; do sleep 0.2; done
echo "Beendet nach ${SECONDS}s"
```

> ⚠️ Beenden von Claude Desktop beendet auch jede Code-Tab-Session, die gerade darin läuft.

**Erwartung:** Prozess beendet sich innerhalb weniger Sekunden; nach Neustart sind Login und
Chatverlauf intakt.

**Notieren:** Gemessene Exit-Zeit, Zustand nach Neustart.

### M6 — Login-Deep-Link

**Ziel:** Wie W6, für macOS — Login (SSO und E-Mail-Code) mit/ohne laufende Default-Instanz.
Zum Starten die Befehle aus M2/M3 wiederverwenden (mit bzw. ohne `--env`/`--args`).

**Erwartung:** Login landet nachvollziehbar im richtigen Profil.

**Notieren:** Wo der Login jeweils landet, je Kombination aus SSO/E-Mail-Code und
laufender/nicht laufender Default-Instanz.

### M7 — Keychain

**Ziel:** Service-Namen im Keychain für das Spike-Profil identifizieren, ohne Secrets
auszugeben.

```zsh
CLAUDE_CONFIG_DIR="$spike/claude" claude
```

Danach in der Session `/login` ausführen. Anschließend:
```zsh
security dump-keychain | grep -i '"svce"' | grep -i claude
```

Zur Kontrolle: normaler Start ohne `CLAUDE_CONFIG_DIR` sollte weiterhin den Firmen-Login
verwenden:
```zsh
claude
```

**Erwartung:** `dump-keychain` zeigt nur Service-Namen/Attribute (keine Passwörter/Tokens), da
kein `-d` verwendet wird. Ein eigener Service-Name-Eintrag für das Spike-Profil ist erkennbar.

**Notieren:** Exakter Service-Name-String für das Custom-Profil, Bestätigung dass `claude` ohne
`CLAUDE_CONFIG_DIR` den Firmen-Login nutzt.

### M8 — `.command`-Datei

**Ziel:** Ein Doppelklick-Startskript bauen und über mehrere Terminal-Apps testen.

Datei erzeugen (LF-Zeilenenden, danach ausführbar machen):
```zsh
cat > "$spike/spike.command" <<'EOF'
#!/bin/zsh
export CLAUDE_CONFIG_DIR="$HOME/.mcacc/spike/claude"
unset CLAUDECODE CLAUDE_CODE_ENTRYPOINT CLAUDE_CODE_SSE_PORT
cd "$HOME" || exit 1
printf '\033]0;%s\007' 'Claude Code · Spike'
"${SHELL:-/bin/zsh}" -l -i -c 'claude'
exec "${SHELL:-/bin/zsh}" -l -i
EOF
chmod 700 "$spike/spike.command"
```

Testen:
```zsh
open -a Terminal "$spike/spike.command"
```
```zsh
open -a iTerm "$spike/spike.command"
```
```zsh
open -na Ghostty --args -e "$spike/spike.command"
```

**Erwartung:** In allen drei Terminal-Apps ist `CLAUDE_CONFIG_DIR` gesetzt, `claude` wird
gefunden und startet, die Shell bleibt danach offen. Kein macOS-Automation-Prompt.

**Notieren:** Verhalten je Terminal-App, ob ein Automation- oder Sicherheits-Prompt erscheint
(insbesondere Ghosttys "allow execute"-Dialog, falls vorhanden).

### M9 — Fallback ohne `--env`

**Ziel:** Nur relevant, falls M1 zeigt, dass `open --env` nicht verfügbar ist.

```zsh
CLAUDE_CONFIG_DIR="$spike/claude" /Applications/Claude.app/Contents/MacOS/Claude --user-data-dir="$spike/desk top ä"
```

**Erwartung:** Direktstart der Binary funktioniert als Ersatz für `open --env`.

**Notieren:** Nur ausfüllen, falls dieser Test nötig war.

### M10 — Signierte MCACC.app (nach Task 14)

**Ziel:** Erst relevant, sobald Task 14 die ad-hoc-signierte `MCACC.app` erzeugt hat. Dann per
Finder-Doppelklick starten und prüfen, ob sie ohne Gatekeeper-Blockade öffnet.

Per Finder doppelklicken oder äquivalent aus dem Terminal starten:
```zsh
open -a "<Pfad zu MCACC.app>"
```

Signatur verifizieren:
```zsh
codesign --verify --deep --strict --verbose=2 "<Pfad zu MCACC.app>"
```

Gatekeeper-Assessment informativ ansehen (bei Ad-hoc-Signatur ohne Notarization ist ein
`rejected` hier normal und kein Fehlschlag):
```zsh
spctl --assess --type execute -vv "<Pfad zu MCACC.app>"
```

**Erwartung:** Die App startet per Finder-Doppelklick ohne Gatekeeper-Blockade (kein
"nicht identifizierter Entwickler"-Dialog, der den Start verhindert); `codesign --verify`
bestätigt eine gültige (Ad-hoc-)Signatur; `spctl --assess` liefert nur zur Information einen
Assessment-Status, ist aber für den eigentlichen Start nicht entscheidend.

**Notieren:** Zu diesem Zeitpunkt noch offen — nach Task 14 nachtragen. Ergebnis der drei
Befehle sowie ob ein Gatekeeper-Dialog erschien und wie er sich wegklicken ließ.

---

## Aufräumen

Reihenfolge beachten: falls W6 die Registry geändert hat, **zuerst** zurücksetzen, dann erst den
Spike-Ordner löschen (das Backup liegt darin).

Windows — falls die Registry noch nicht zurückgesetzt ist:
```powershell
reg import "$spike\claude-protocol-backup.reg"
```

Windows — Spike-Ordner löschen:
```powershell
Remove-Item -Recurse -Force $spike
```

macOS — Spike-Ordner löschen:
```zsh
rm -rf "$spike"
```

Optional: Spike-Accounts in Claude Desktop und CLI ausloggen (`/logout` in der jeweiligen
Session, bzw. über die Desktop-UI abmelden).
