using Mcacc.Platform;

namespace Mcacc.App;

/// <summary>What the command line of a start asks for.</summary>
internal static class StartupArgs
{
    /// <summary>A start at login: MCACC comes up in the tray alone.</summary>
    public static bool IsHidden(IEnumerable<string>? args) =>
        args?.Contains(LoginItem.HiddenSwitch, StringComparer.OrdinalIgnoreCase) == true;

    /// <summary>
    /// A second start brings the running window up, but not a start at login: it finds MCACC already running and has
    /// nothing to show.
    /// </summary>
    public static bool ShouldSignalExisting(IEnumerable<string>? args) => !IsHidden(args);
}
