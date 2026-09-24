using System.Globalization;
using System.Text.RegularExpressions;
using Mcacc.Core.Stats;

namespace Mcacc.App.Localization;

public static partial class Texts
{
    /// <summary>The info page formats with a fixed culture: the app is German, whatever the machine says.</summary>
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    // Info page
    public const string Info = "Info & Statistik";
    public const string InfoSubtitle = "Alles, was sich über diesen Account auslesen lässt.";
    public const string InfoAccount = "Konto";
    public const string InfoLimits = "Limits";
    public const string InfoUsage = "Claude Code Nutzung";

    public const string InfoEmail = "E-Mail";
    public const string InfoName = "Name";
    public const string InfoPlan = "Plan";
    public const string InfoOrganization = "Organisation";
    public const string InfoRole = "Rolle";
    public const string InfoBilling = "Abrechnung";
    public const string InfoRateLimitTier = "Rate-Limit-Stufe";
    public const string InfoCreatedAt = "Konto erstellt";
    public const string InfoTokenExpires = "Token gültig bis";
    public const string InfoScopes = "Berechtigungen";
    public const string InfoStartups = "Claude-Code-Starts";
    public const string InfoInstallMethod = "Installation";
    public const string InfoProjects = "Projekte";
    public const string InfoDesktop = "Claude Desktop";
    public const string InfoDesktopNotRunning = "läuft nicht";
    public const string InfoNoAccountData = "Noch keine Kontodaten – einmal mit diesem Account anmelden.";
    public const string InfoNoLimits = "Keine Limits verfügbar.";

    public const string InfoHistory = "Verlauf";
    public const string InfoHistoryDay = "24 h";
    public const string InfoHistoryWeek = "7 Tage";
    public const string InfoNoHistory = "Noch kein Verlauf – er wächst, solange MCACC offen ist.";
    public const string InfoHistoryDayStart = "vor 24 h";
    public const string InfoHistoryWeekStart = "vor 7 Tagen";
    public const string InfoNow = "jetzt";

    public const string InfoTokens = "Tokens";
    public const string InfoCost = "Kosten";
    public const string InfoCostNote = "API-Äquivalent, Schätzung";
    public const string InfoSessions = "Sessions";
    public const string InfoMessages = "Nachrichten";
    public const string InfoActiveDays = "Aktive Tage";

    public const string InfoTokensPerDay = "Tokens pro Tag";
    public const string InfoTokenKinds = "Token-Arten";
    public const string InfoTopProjects = "Top-Projekte";
    public const string InfoModels = "Modelle";

    public const string InfoKindInput = "Input";
    public const string InfoKindOutput = "Output";
    public const string InfoKindCacheWrite = "Cache-Write";
    public const string InfoKindCacheRead = "Cache-Read";

    public const string InfoColumnModel = "Modell";
    public const string InfoColumnTokens = "Tokens";
    public const string InfoColumnShare = "Anteil";
    public const string InfoColumnCost = "≈ Kosten";
    public const string InfoOtherModels = "Andere";

    public const string InfoNoSessions = "Keine Claude-Code-Sessions in diesem Zeitraum.";
    public const string InfoScanning = "Logs werden gelesen …";
    public const string InfoScanFailed = "Die Claude-Code-Logs ließen sich nicht lesen.";
    public const string InfoNoProject = "(ohne Projekt)";
    public const string InfoNoValue = "—";

    private const string WeekPrefix = "seven_day_";

    public static string TokenAmount(long tokens) => tokens switch
    {
        < 1_000 => tokens.ToString("0", De),
        < 1_000_000 => Scaled(tokens / 1e3, "k"),
        < 1_000_000_000 => Scaled(tokens / 1e6, "M"),
        _ => Scaled(tokens / 1e9, "Mrd."),
    };

    public static string Count(long value) => value.ToString("N0", De);

    public static string Cost(decimal usd) => usd is > 0 and < 0.01m
        ? $"≈ < {0.01m.ToString("N2", De)} $"
        : $"≈ {usd.ToString("N2", De)} $";

    public static string Percent(double value) => $"{value.ToString("0", De)} %";

    /// <param name="fraction">0..1</param>
    public static string Share(double fraction) => $"{(fraction * 100).ToString("0.0", De)} %";

    /// <returns>Null when neither the profile nor the credentials name a plan.</returns>
    public static string? Plan(bool? hasMax, bool? hasPro, string? subscriptionType)
    {
        if (hasMax == true)
        {
            return "Claude Max";
        }

        if (hasPro == true)
        {
            return "Claude Pro";
        }

        return subscriptionType?.ToLowerInvariant() switch
        {
            null or "" => null,
            "max" => "Claude Max",
            "pro" => "Claude Pro",
            "team" => "Claude Team",
            "enterprise" => "Claude Enterprise",
            _ => Humanize(subscriptionType),
        };
    }

    /// <summary>"stripe_subscription" → "Stripe subscription": for API values without a German name.</summary>
    public static string Humanize(string value)
    {
        var text = value.Replace('_', ' ').Trim();
        return text.Length == 0 ? text : char.ToUpper(text[0], De) + text[1..];
    }

    /// <param name="key">The raw API name, e.g. <c>seven_day_opus</c>.</param>
    public static string UsageExtraWindow(string key) => key.StartsWith(WeekPrefix, StringComparison.Ordinal)
        ? $"{UsageSevenDay} {Humanize(key[WeekPrefix.Length..])}"
        : Humanize(key);

    public static string InfoDate(DateTimeOffset at) => at.ToLocalTime().ToString("dd.MM.yyyy", De);

    public static string InfoDateTime(DateTimeOffset at) => at.ToLocalTime().ToString("dd.MM.yyyy HH:mm", De);

    public static string InfoSince(DateTimeOffset start, DateTimeOffset now)
    {
        var local = start.ToLocalTime();
        return local.Date == now.ToLocalTime().Date
            ? $"läuft seit {local.ToString("HH:mm", De)}"
            : $"läuft seit {local.ToString("dd.MM. HH:mm", De)}";
    }

    /// <param name="cwd">The working directory of the session, as the log wrote it.</param>
    public static string ProjectName(string cwd)
    {
        var trimmed = cwd.TrimEnd('/', '\\');
        if (trimmed.Length == 0)
        {
            return InfoNoProject;
        }

        var slash = trimmed.LastIndexOfAny(['/', '\\']);
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    /// <summary>"claude-opus-4-5-20251101" → "Opus 4.5"; unknown shapes stay as they are.</summary>
    public static string ModelName(string model)
    {
        var name = ModelPricing.Normalize(model);
        var match = FamilyFirst().Match(name);
        if (!match.Success)
        {
            match = VersionFirst().Match(name);
        }

        if (!match.Success)
        {
            return model;
        }

        var family = Humanize(match.Groups["family"].Value);
        var minor = match.Groups["minor"];
        return minor.Success
            ? $"{family} {match.Groups["major"].Value}.{minor.Value}"
            : $"{family} {match.Groups["major"].Value}";
    }

    public static string DayLabel(DateOnly day) => day.ToString("dd.MM.", De);

    public static string DayTooltip(DailyUsage day)
    {
        var lines = day.TokensByModel
            .Where(entry => entry.Value > 0)
            .OrderByDescending(entry => entry.Value)
            .Select(entry => $"{ModelName(entry.Key)}: {TokenAmount(entry.Value)}");
        return string.Join('\n', [$"{day.Day.ToString("dddd, dd.MM.", De)} · {TokenAmount(day.Total)} {InfoTokens}", .. lines]);
    }

    public static string HistoryTooltip(DateTimeOffset at, double? fiveHour, double? sevenDay) =>
        $"{at.ToLocalTime().ToString("dd.MM. HH:mm", De)}\n"
        + $"{UsageFiveHour}: {PercentOrDash(fiveHour)}\n"
        + $"{UsageSevenDay}: {PercentOrDash(sevenDay)}";

    public static string AmountTooltip(string label, long value, long total) => total > 0
        ? $"{label}: {TokenAmount(value)} · {Share((double)value / total)}"
        : $"{label}: {TokenAmount(value)}";

    /// <param name="project">The full working directory; the bar list shows only its last folder.</param>
    public static string ProjectTooltip(string project, long tokens, decimal cost) =>
        $"{(project.Length > 0 ? project : InfoNoProject)}\n{TokenAmount(tokens)} {InfoTokens} · {Cost(cost)}";

    public static string InfoDays(int days) => $"{days} Tage";

    public static string InfoActiveDaysOf(int active, int days) => $"{active} von {days}";

    /// <param name="progress">0..1</param>
    public static string InfoScanProgress(double progress) => $"{InfoScanning} {Percent(progress * 100)}";

    public static string InfoUnpriced(long tokens) => $"{TokenAmount(tokens)} Tokens ohne bekannten Preis";

    private static string Scaled(double value, string unit) =>
        $"{value.ToString(value < 100 ? "0.#" : "0", De)} {unit}";

    private static string PercentOrDash(double? value) => value is { } percent ? Percent(percent) : InfoNoValue;

    [GeneratedRegex(@"^claude-(?<family>[a-z]+)-(?<major>\d+)(?:-(?<minor>\d+))?$")]
    private static partial Regex FamilyFirst();

    [GeneratedRegex(@"^claude-(?<major>\d+)(?:-(?<minor>\d+))?-(?<family>[a-z]+)$")]
    private static partial Regex VersionFirst();
}
