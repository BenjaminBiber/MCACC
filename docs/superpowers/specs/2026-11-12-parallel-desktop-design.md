# MCACC – Mehrere Accounts gleichzeitig in der Desktop-App

Ergänzt `2026-09-23-mcacc-design.md` und `2026-09-25-tray-design.md`. Plan: `docs/superpowers/plans/2026-11-12-parallel-desktop.md`.

## Context

Heute läuft die Desktop-App (Claude Desktop, ChatGPT) mit genau einem Account zur Zeit. Wer einen zweiten Account startet, bekommt eine Rückfrage; danach beendet MCACC die laufende Instanz und startet die App neu. Gewünscht ist: Fenster 1 mit Account 1, Fenster 2 mit Account 2, beide gleichzeitig.

Technisch steht dem wenig im Weg. MCACC startet jeden Profil-Account schon mit eigenem `--user-data-dir`, und die Einzelinstanz-Sperre der App gilt je Datenordner. Dass nur eine Instanz läuft, ist eine Regel von MCACC selbst: `DesktopLaunchService` meldet Instanzen anderer Accounts als Konflikt und beendet sie vor dem Start. Die Erkennung (`DesktopInstanceDetector`) unterscheidet mehrere Instanzen bereits und ordnet sie ihren Accounts zu.

Offen ist, wie sich die Apps im Dauerbetrieb nebeneinander verhalten (Prüfpunkt W4 in `docs/spike-results.md` ist nie ausgefüllt worden). Deshalb kommt der Parallelbetrieb als abschaltbare Einstellung, standardmäßig aus.

## Entscheidungen (Annahmen von Claude, 2026-11-12 – noch nicht mit Benjamin geklärt)

| Thema | Entscheidung |
|---|---|
| Modus | Opt-in über die Einstellung **Mehrere Accounts gleichzeitig öffnen**, standardmäßig aus. Aus heißt: Verhalten wie bisher, ohne jede Änderung. |
| Speicherort | `AppSettings.AllowParallelDesktop`, in der `config.json` als `allowParallelDesktop`. Konfigurationen ohne den Schlüssel lesen sich als „aus“. |
| Wo die Regel sitzt | In Core: `DesktopLaunchService` fragt bei jeder Prüfung und jedem Start einen `Func<bool>`. `IDesktopLauncher` bleibt unverändert, damit Zielauswahl, Tray und deren Tests nichts davon merken. |
| Derselbe Account läuft schon | Wie bisher: Der Start geht durch, die App holt ihr Fenster nach vorne. |
| Instanzen unbekannter Profile | Bleiben im Parallelbetrieb offen. |
| Anbieter | Gilt für Claude Desktop und ChatGPT gleich. Für ChatGPT ist es ungeprüft. |
| Anmeldung über den Browser | Je Anbieter gibt es höchstens eine Anmelde-Überwachung. Die Anmeldung geht an den Account, der zuletzt gestartet wurde. |
| Tray und Account-Kacheln | Unverändert. „Läuft“ steht schon heute bei jedem Account, dessen Instanz läuft. |
| Nicht im Umfang | Fenster anordnen oder beschriften, eigene Taskleisten-Icons je Account, den `claude://`-Handler umbiegen, eine Obergrenze für die Zahl der Instanzen. |

## User Stories

- **US1 – Zwei Accounts nebeneinander.** Als Nutzer mit mehreren Accounts will ich die Desktop-App für zwei Accounts gleichzeitig offen haben, ohne dass MCACC die erste Instanz beendet.
  - Szenario 1: Einstellung an, „Standard“ läuft, ich starte „Privat“ → keine Rückfrage, „Standard“ bleibt offen, „Privat“ öffnet sich in einem eigenen Fenster.
  - Szenario 2: Einstellung aus, „Standard“ läuft, ich starte „Privat“ → Rückfrage wie bisher, nach „Neu starten“ läuft nur noch „Privat“.
  - Szenario 3: Einstellung an, „Privat“ läuft, ich starte „Privat“ erneut → das Fenster von „Privat“ kommt nach vorne, es entsteht keine zweite Instanz.
- **US2 – Den Modus kennen und umschalten.** Als Nutzer will ich den Parallelbetrieb in den Einstellungen ein- und ausschalten und vor dem Start sehen, was passieren wird.
  - Szenario 1: Der Schalter steht auf der Einstellungsseite, Umschalten wirkt beim nächsten Start ohne Neustart von MCACC.
  - Szenario 2: Die Zielauswahl sagt bei eingeschaltetem Parallelbetrieb, dass sich ein weiteres Fenster öffnet, sonst wie bisher, dass nach Rückfrage neu gestartet wird.
- **US3 – Die Anmeldung landet beim richtigen Account.** Als Nutzer will ich, dass eine Anmeldung über den Browser nicht bei einem Account landet, den ich vorher gestartet habe.
  - Szenario 1: Ich starte „Privat“ und gleich danach „Arbeit“. Die nächste Anmeldung geht an „Arbeit“; die Überwachung von „Privat“ fasst sie nicht an.
  - Szenario 2: Ich starte „Privat“ und danach „Standard“. Die Überwachung von „Privat“ endet; eine Anmeldung landet im Standardprofil, wo sie hingehört.

## Anforderungen

- **FR-001** `AppSettings` hat die Einstellung `AllowParallelDesktop` (`bool`, Standard `false`). Sie wird als `allowParallelDesktop` gespeichert; fehlt der Schlüssel, gilt `false`.
- **FR-002** Ist die Einstellung an, meldet die Prüfung vor dem Start (`PrepareAsync`) keinen Konflikt, auch wenn Instanzen anderer Accounts oder unbekannter Profile laufen: Ergebnis `Ready`. Läuft der gewählte Account selbst, bleibt es bei `RunningSameAccount`.
- **FR-003** Ist die Einstellung an, beendet der Start (`LaunchAsync`) nichts, auch nicht mit `quitConflicts: true`, und fragt die laufenden Instanzen dafür nicht ab. Er startet die App wie bisher 500 ms nach dem Aufruf mit dem Datenordner des gewählten Accounts.
- **FR-004** Die Einstellung wird bei jeder Prüfung und jedem Start neu gelesen. Umschalten wirkt ohne Neustart von MCACC.
- **FR-005** Die Einstellungsseite zeigt den Schalter „Mehrere Accounts gleichzeitig öffnen“ mit einem Hinweistext darunter. Umschalten speichert sofort.
- **FR-006** Die Zielauswahl zeigt bei eingeschalteter Einstellung den Untertitel „Läuft <App> schon mit einem anderen Account, öffnet sich ein weiteres Fenster daneben.“, sonst den bisherigen.
- **FR-007** Je Anbieter läuft höchstens eine Anmelde-Überwachung. Eine neue Überwachung löst die laufende desselben Anbieters ab; die abgelöste endet mit `Superseded`, ohne etwas zu beenden oder zu starten. Das gilt auch, wenn die neue selbst keine braucht (Standard-Account).
- **FR-008** Ein Start des Standard-Accounts beendet die laufende Überwachung seines Anbieters und nimmt deren Hinweis vom Fenster. Lief keine, bleibt ein vorhandener Banner stehen.
- **FR-009** Eine abgelöste Überwachung lässt den Hinweis stehen und blendet das Fenster nicht aus; beides gehört jetzt dem neueren Start.
- **FR-010** Die README beschreibt den Parallelbetrieb, die Regel zur Anmeldung und die Grenzen.
- **FR-011** Der Parallelstart ist von Hand geprüft und der Befund steht in `docs/spike-results.md` unter W4.

## Start im Parallelbetrieb

```
Zielauswahl / Tray
      │  LaunchDesktopAsync(account)
      ▼
LaunchCoordinator ── PrepareAsync ──► DesktopLaunchService ── allowParallel()? ──► AccountService.Config.Settings
      │                                   │ an:  Ready / RunningSameAccount (nie Conflict)
      │                                   │ aus: wie bisher, Conflict bei fremder Instanz
      │  kein Dialog bei Ready
      ▼
LaunchAsync(quitConflicts: false) ──► an: keine Erkennung, nichts beenden, nach 500 ms starten
```

Der `LaunchCoordinator` bleibt für den Start unverändert: Ohne `Conflict` zeigt er keinen Dialog und ruft `LaunchAsync` mit `quitConflicts: false` auf. Die Kachel „Claude Desktop“ in der Zielauswahl zeigt dadurch von selbst „App mit diesem Account starten“ statt „Läuft mit … – wird beendet“.

## Anmeldung im Parallelbetrieb

Eine Anmeldung über den Browser kommt als `claude://`-Adresse zurück. Die öffnet das Betriebssystem mit dem registrierten Handler, also ohne `--user-data-dir`. Die Adresse nennt keinen Account. `DesktopLoginRedirector` fängt so einen Start ab und übergibt die Adresse an das Profil des Accounts, den er überwacht.

Heute endet eine Überwachung von selbst, sobald ihre Instanz beendet wird; mehr als eine läuft praktisch nie. Im Parallelbetrieb bleiben die Instanzen offen, und zwei Überwachungen würden um dieselbe Anmeldung streiten. Deshalb:

- Je Anbieter merkt sich der `DesktopLoginRedirector` die laufende Überwachung. Eine neue ersetzt sie; die ältere bemerkt das bei ihrem nächsten Blick und endet mit `Superseded`.
- Ein Start des Standard-Accounts braucht keine Überwachung, beendet aber die laufende (`StopWatching`): Die Anmeldung landet dann im Standardprofil, und das ist jetzt richtig.
- Der `LaunchCoordinator` behandelt `Superseded` als „nicht mehr meine Sache“: kein Aufräumen des Hinweises, kein Ausblenden.

Die Regel gilt unabhängig von der Einstellung; sie ist auch im bisherigen Betrieb richtig.

## Risiken und Grenzen

- **Anmeldung, während der Standard-Account läuft.** Läuft im Parallelbetrieb eine Instanz im Standardprofil, reicht der vom Betriebssystem gestartete Handler die Adresse womöglich sofort an sie weiter, bevor MCACC ihn abfangen kann. Dann landet die Anmeldung eines Profil-Accounts im Standardprofil. Abhilfe bis zur Prüfung: einen Account anmelden, solange die Instanz des Standard-Accounts nicht läuft. Die Prüfung von Hand (FR-011) klärt das.
- **Nur der zuletzt gestartete Account wird überwacht.** Wer „Privat“ und „Arbeit“ startet und sich dann in „Privat“ anmeldet, bekommt die Anmeldung in „Arbeit“. Die README nennt die Reihenfolge: starten, anmelden, dann den nächsten Account starten.
- **Geteilte Dinge außerhalb des Datenordners.** Globale Tastenkürzel, Tray-Icons der App, Autostart-Einträge und Updates gehören der App und nicht dem Profil. Mehrere Instanzen können sich dort in die Quere kommen.
- **Speicher.** Jede Instanz braucht eigenen Arbeitsspeicher; jedes Profil lädt eigene Binaries.
- **ChatGPT** ist ungeprüft.

## Tests

- Core: `AppSettingsTests`, `McaccJsonContextTests` (Standardwert, Schreiben, Lesen ohne Schlüssel); `DesktopLaunchServiceTests` (Prüfung und Start im Parallelbetrieb, Einstellung wird je Start gefragt); `DesktopLoginRedirectorTests` (ablösen, stoppen, anderer Anbieter, Standard-Account).
- App: `SettingsViewModelTests` und `PageRenderTests` (Schalter), `TargetPickerViewModelTests` (Untertitel), `LaunchCoordinatorTests` (Standard-Start beendet die Überwachung, abgelöste Überwachung räumt nicht auf).
- Von Hand: Parallelstart unter Windows, Befund unter W4 in `docs/spike-results.md`. MCACC dafür nie aus dem Code-Tab von Claude Desktop starten.
