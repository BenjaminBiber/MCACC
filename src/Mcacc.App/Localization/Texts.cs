namespace Mcacc.App.Localization;

/// <summary>Every string the user sees. Core returns enums and result types, never German text.</summary>
public static partial class Texts
{
    // Account picker
    public const string AccountPickerTitle = "Mit welchem Account?";
    public const string AccountPickerSubtitle = "Wähle das Konto, mit dem Claude starten soll.";
    public const string AddAccount = "Account hinzufügen";
    public const string RefreshUsage = "Limits aktualisieren";
    public const string Settings = "Einstellungen";
    public const string NotLoggedIn = "Noch nicht angemeldet";
    public const string RunningBadge = "Läuft";
    public const string More = "Mehr";
    public const string Edit = "Bearbeiten";
    public const string Remove = "Entfernen";
    public const string OpenFolder = "Ordner öffnen";

    // Usage
    public const string UsageFiveHour = "5h";
    public const string UsageSevenDay = "Woche";
    public const string UsageExpired = "Abgelaufen – Claude Code starten zum Aktualisieren";

    public static string UsageAsOf(DateTimeOffset fetchedAt) => $"Stand {fetchedAt.ToLocalTime():HH:mm}";

    public static string UsageWrongAccount(string? tokenEmail) => tokenEmail is null
        ? "Claude Code ist hier mit einem anderen Account angemeldet – im Profil neu anmelden"
        : $"Claude Code ist hier mit {tokenEmail} angemeldet – im Profil neu anmelden";

    public static string UsageReset(DateTimeOffset resetsAt)
    {
        var local = resetsAt.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date ? $"Reset {local:HH:mm}" : $"Reset {local:ddd HH:mm}";
    }

    // Target picker
    public const string TargetPickerTitle = "Was möchtest du öffnen?";
    public const string TargetPickerSubtitle =
        "Läuft Claude Desktop mit einem anderen Account, wird es nach Rückfrage neu gestartet.";
    public const string Back = "Zurück";
    public const string TargetDesktop = "Claude Desktop";
    public const string TargetCode = "Claude Code";
    public const string DesktopReady = "App mit diesem Account starten";
    public const string DesktopRunningSameAccount = "Läuft bereits – wird in den Vordergrund geholt";
    public const string DesktopNotInstalled = "Nicht installiert";
    public const string DesktopConflictUnknownOwner = "Läuft mit einem anderen Profil – wird beendet";
    public const string CodeNoTerminal = "Kein Terminal gefunden";

    public static string DesktopConflict(string ownerName) => $"Läuft mit „{ownerName}“ – wird beendet";

    public static string CodeInTerminal(string terminalName) => $"im {terminalName} öffnen";

    // Conflict dialog
    public const string ConflictDialogTitle = "Claude läuft bereits";
    public const string ConflictDialogRestart = "Neu starten";
    public const string Cancel = "Abbrechen";

    public static string ConflictDialogText(string ownerName, string targetName) =>
        $"Claude läuft gerade mit „{ownerName}“. Beenden und mit „{targetName}“ neu starten? "
        + "Nicht gesendete Eingaben gehen verloren.";

    // Busy and errors
    public const string BusyQuitting = "Claude wird beendet…";
    public const string BusyStarting = "Claude wird gestartet…";
    public const string ErrorDesktopNotFound = "Claude Desktop wurde nicht gefunden.";
    public const string ErrorQuitFailed = "Claude konnte nicht beendet werden. Bitte manuell schließen und erneut versuchen.";
    public const string ErrorDesktopStartFailed = "Claude Desktop konnte nicht gestartet werden.";
    public const string ErrorNoTerminal = "Kein unterstütztes Terminal gefunden.";
    public const string ErrorTerminalStartFailed = "Das Terminal konnte nicht gestartet werden.";
    public const string HintLoginWatch =
        "Claude Desktop läuft. Meldest du dich über den Browser an, leitet MCACC die Anmeldung auf dieses Profil "
        + "um und schließt sich danach von selbst.";
    public const string ErrorLoginRedirectFailed =
        "Die Anmeldung konnte nicht auf dieses Profil umgeleitet werden. Bitte in Claude Desktop erneut anmelden.";
    public const string HintClaudeCliMissing =
        "Hinweis: „claude“ wurde nicht im PATH gefunden. Installiere Claude Code, falls das Terminal einen Fehler zeigt.";
    public const string ErrorRunningWithThisAccount =
        "Claude Desktop läuft noch mit diesem Account. Bitte zuerst beenden.";
    public const string ErrorConfigReadOnly =
        "Die Konfiguration stammt von einer neueren MCACC-Version und wird nur gelesen.";
    public const string DismissError = "Schließen";

    // Settings page
    public const string SettingsTitle = "Einstellungen";
    public const string SettingsTerminal = "Terminal für Claude Code";
    public const string SettingsShell = "Shell im Windows Terminal";
    public const string SettingsShellAuto = "Automatisch";
    public const string SettingsShellPwsh = "PowerShell 7";
    public const string SettingsShellWindowsPowerShell = "Windows PowerShell";
    public const string SettingsShellCmd = "Eingabeaufforderung";
    public const string SettingsCloseAfterLaunch = "Nach dem Start schließen";
    public const string SettingsDataFolder = "Datenordner";
    public const string RevealInExplorer = "Im Explorer öffnen";
    public const string RevealInFinder = "Im Finder öffnen";

    public static string SettingsTerminalDefault(string defaultName) => $"Standard ({defaultName})";

    public static string Version(string version) => $"Version {version}";

    // Account editor
    public const string EditorCreateTitle = "Account hinzufügen";
    public const string EditorEditTitle = "Account bearbeiten";
    public const string EditorName = "Name";
    public const string EditorNameWatermark = "z. B. Privat";
    public const string EditorNameEmpty = "Gib einen Namen ein.";
    public const string EditorNameTooLong = "Höchstens 40 Zeichen.";
    public const string EditorNameDuplicate = "Diesen Namen gibt es schon.";
    public const string EditorColor = "Farbe";
    public const string EditorCopyFrom = "Einstellungen übernehmen von";
    public const string EditorCopyNothing = "Nichts";
    public const string EditorRewritePaths = "Pfade anpassen";
    public const string EditorNotAvailableSuffix = " (nicht vorhanden)";
    public const string EditorCreate = "Anlegen";
    public const string EditorSave = "Speichern";

    public static string EditorNotCopied(IEnumerable<string> items) => $"Nicht übernommen: {string.Join(", ", items)}";

    // Copy items
    public const string CopyClaudeMd = "CLAUDE.md";
    public const string CopySettingsJson = "settings.json";
    public const string CopyAgents = "Agents";
    public const string CopySkills = "Skills";
    public const string CopyCommands = "Commands";
    public const string CopyScripts = "Scripts";
    public const string CopyDesktopMcp = "MCP-Server (Claude Desktop)";

    // Remove dialog
    public const string RemoveDialogText = "Der Account wird aus der Liste entfernt.";
    public const string RemoveDialogDeleteData = "Daten auch löschen";
    public const string RemoveDialogDeleteWarning =
        "Login, Verlauf und Einstellungen dieses Accounts werden endgültig gelöscht.";

    public static string RemoveDialogTitle(string accountName) => $"„{accountName}“ entfernen?";
}
