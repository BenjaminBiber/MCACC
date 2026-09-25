using Mcacc.Core.Model;

namespace Mcacc.App.Localization;

public static partial class Texts
{
    /// <summary>Windows cuts a notification-area tooltip after 127 characters.</summary>
    public const int TrayToolTipMaxLength = 127;

    // Tray menu
    public const string TrayOpen = "MCACC öffnen";
    public const string TraySettings = "Einstellungen …";
    public const string TrayQuit = "MCACC beenden";
    public const string TrayLaunchDesktop = "Claude Desktop starten";
    public const string TrayLaunchCode = "Claude Code starten";
    public const string TrayRunning = "läuft";
    public const string TrayNoUsage = "Keine Limits bekannt";

    public static string SettingsTrayHint(OsKind os) => os == OsKind.MacOS
        ? "MCACC bleibt in der Menüleiste. Beenden über das Menüleisten-Symbol oder ⌘Q."
        : "MCACC bleibt im Infobereich der Taskleiste. Beenden über das Symbol dort.";

    /// <summary>The line of an account in the tray menu, e.g. <c>Privat · 5h 42 % · Woche 10 % · läuft</c>.</summary>
    public static string TrayAccountHeader(string name, double? fiveHour, double? sevenDay, bool isRunning)
    {
        var parts = new List<string> { name };
        if (fiveHour is { } five)
        {
            parts.Add($"{UsageFiveHour} {Percent(five)}");
        }

        if (sevenDay is { } seven)
        {
            parts.Add($"{UsageSevenDay} {Percent(seven)}");
        }

        if (isRunning)
        {
            parts.Add(TrayRunning);
        }

        return string.Join(" · ", parts);
    }

    /// <summary>One limit inside an account's submenu, e.g. <c>5h: 42 % · Reset 14:30</c>.</summary>
    public static string TrayUsageLine(string label, double utilization, DateTimeOffset? resetsAt) => resetsAt is { } reset
        ? $"{label}: {Percent(utilization)} · {UsageReset(reset)}"
        : $"{label}: {Percent(utilization)}";

    /// <summary>
    /// The tooltip of the tray icon: the app, and the limits of the account used last when they are known, cut to
    /// what Windows shows.
    /// </summary>
    public static string TrayToolTip(string? accountName, double? fiveHour, double? sevenDay)
    {
        var text = accountName is null
            ? "MCACC"
            : fiveHour is null && sevenDay is null
                ? $"MCACC – {accountName}"
                : $"MCACC – {TrayAccountHeader(accountName, fiveHour, sevenDay, isRunning: false)}";
        return text.Length <= TrayToolTipMaxLength ? text : string.Concat(text.AsSpan(0, TrayToolTipMaxLength - 1), "…");
    }
}
