namespace Mcacc.App.Localization;

public static partial class Texts
{
    // Claude limits recorded through the status line
    public const string SettingsRecordClaudeLimits = "Claude-Limits über die Statusline mitschreiben";

    public const string SettingsRecordClaudeLimitsHint =
        "MCACC trägt sich als Statusline in die settings.json der Claude-Accounts ein und merkt sich die Limits, die "
        + "Claude Code nach jeder Antwort meldet. Kann MCACC die Limits nicht abrufen, zeigt die Kachel diesen Stand. "
        + "Die Desktop-App meldet keine Limits.";

    public static string SettingsStatusLineForeign(IEnumerable<string> names) =>
        $"Eigene Statusline, daher ausgelassen: {string.Join(", ", names)}";
}
