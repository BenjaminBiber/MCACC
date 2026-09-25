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
    public static LoginItemSync ForProcess(ILoginItem item, ILog log) => new(
        item,
        Environment.ProcessPath,
        IsActive(IsDebugBuild, Environment.GetEnvironmentVariable("MCACC_HOME")),
        log);

    /// <summary>
    /// A debug build would make the login start the exe in <c>bin/Debug</c>, and a run under its own data folder is a
    /// test that must not touch the real entry.
    /// </summary>
    internal static bool IsActive(bool isDebugBuild, string? mcaccHome) =>
        !isDebugBuild && string.IsNullOrWhiteSpace(mcaccHome);

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
