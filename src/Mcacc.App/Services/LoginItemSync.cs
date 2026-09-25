using System.Security;
using Mcacc.Core.Logging;
using Mcacc.Platform;

namespace Mcacc.App.Services;

/// <summary>
/// Keeps the login entry in step with the setting, at each start and each switch. The entry always names the running
/// MCACC, so a moved app still starts. A failure is logged: the entry must never cost MCACC its start.
/// </summary>
public sealed class LoginItemSync(ILoginItem item, string? executablePath, bool isActive, ILog log)
{
#if DEBUG
    private const bool IsDebugBuild = true;
#else
    private const bool IsDebugBuild = false;
#endif

    /// <summary>For the running process: the exe it was started from, active only in a real installation.</summary>
    public static LoginItemSync ForProcess(ILoginItem item, ILog log) => ForProcess(
        item,
        log,
        Environment.ProcessPath,
        [
            Path.GetTempPath(),
            // %TEMP% can be an 8.3 short path (e.g. C:\Users\MAXMUS~1\…) while the exe path is the long form.
            Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
        ]);

    /// <summary>Testable seam: the process path and the temp dirs come in rather than from the environment.</summary>
    internal static LoginItemSync ForProcess(ILoginItem item, ILog log, string? processPath, IEnumerable<string> tempDirs)
    {
        var isActive = IsActive(IsDebugBuild, Environment.GetEnvironmentVariable("MCACC_HOME"));
        if (processPath is { } path && IsTransient(path, tempDirs))
        {
            log.Info($"MCACC runs from {path}, which is gone by the next login; the login entry stays as it is.");
            return new LoginItemSync(item, null, isActive, log);
        }

        return new LoginItemSync(item, processPath, isActive, log);
    }

    /// <summary>
    /// A debug build would make the login start the exe in <c>bin/Debug</c>, and a run under its own data folder is a
    /// test that must not touch the real entry.
    /// </summary>
    internal static bool IsActive(bool isDebugBuild, string? mcaccHome) =>
        !isDebugBuild && string.IsNullOrWhiteSpace(mcaccHome);

    /// <summary>
    /// True when the exe runs from a place that will not exist at the next login: a zip opened in Explorer (runs from
    /// a temp dir) or a macOS app under App Translocation (runs from a random, per-launch folder).
    /// </summary>
    internal static bool IsTransient(string path, IEnumerable<string> tempDirs)
    {
        if (path.Contains("/AppTranslocation/", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var dir in tempDirs)
        {
            var trimmed = dir.TrimEnd('\\', '/');
            if (path.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase) &&
                path.Length > trimmed.Length &&
                path[trimmed.Length] is '\\' or '/')
            {
                return true;
            }
        }

        return false;
    }

    public void Apply(bool launchAtLogin)
    {
        if (!isActive)
        {
            return;
        }

        try
        {
            if (!launchAtLogin)
            {
                item.Disable();
            }
            else if (executablePath is { } path)
            {
                item.Enable(path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            log.Warn($"Updating the login entry (launch at login: {launchAtLogin}) failed.", e);
        }
    }
}
