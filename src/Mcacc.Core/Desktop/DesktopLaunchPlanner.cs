using System.Collections.ObjectModel;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Desktop;

public static class DesktopLaunchPlanner
{
    private const string Open = "/usr/bin/open";
    private const string ConfigDir = EnvironmentScrub.ClaudeConfigDirVariable;

    private static readonly IReadOnlyList<string> ScrubWithConfigDir = [.. EnvironmentScrub.Always, ConfigDir];

    /// <param name="homeDir">The working directory of Claude Desktop.</param>
    /// <param name="deepLinkUrl">
    /// A <c>claude://</c> URL to hand to the profile. Electron holds its single-instance lock per user data
    /// directory, so a running instance of that profile receives the URL and this start exits again.
    /// </param>
    public static ProcessSpec PlanWindows(
        DesktopInstallation inst,
        AccountPaths paths,
        string homeDir,
        string? deepLinkUrl = null)
    {
        var arguments = Arguments(paths.DesktopUserDataDir, deepLinkUrl);
        return paths.ClaudeConfigDir is { } configDir
            ? new ProcessSpec(inst.LaunchPath, arguments, homeDir, new Dictionary<string, string> { [ConfigDir] = configDir }, EnvironmentScrub.Always)
            : new ProcessSpec(inst.LaunchPath, arguments, homeDir, ReadOnlyDictionary<string, string>.Empty, ScrubWithConfigDir);
    }

    /// <param name="deepLinkUrl">See <see cref="PlanWindows"/>.</param>
    public static ProcessSpec PlanMac(DesktopInstallation inst, AccountPaths paths, string? deepLinkUrl = null)
    {
        List<string> arguments = ["-n", "-a", inst.LaunchPath];
        if (paths.ClaudeConfigDir is { } configDir)
        {
            arguments.AddRange(["--env", $"{ConfigDir}={configDir}"]);
        }

        var appArguments = Arguments(paths.DesktopUserDataDir, deepLinkUrl);
        if (appArguments.Count > 0)
        {
            arguments.Add("--args");
            arguments.AddRange(appArguments);
        }

        return new ProcessSpec(Open, arguments, WorkingDirectory: null, ReadOnlyDictionary<string, string>.Empty, ScrubWithConfigDir);
    }

    /// <summary>The switch comes first, so Chromium has the profile before it looks at the URL behind it.</summary>
    private static List<string> Arguments(string? userDataDir, string? deepLinkUrl)
    {
        List<string> arguments = [];
        if (userDataDir is not null)
        {
            arguments.Add(UserDataDirArgument(userDataDir));
        }

        if (deepLinkUrl is not null)
        {
            arguments.Add(deepLinkUrl);
        }

        return arguments;
    }

    private static string UserDataDirArgument(string userDataDir) => $"{ClaudeDesktopConstants.UserDataDirSwitch}={userDataDir}";
}
