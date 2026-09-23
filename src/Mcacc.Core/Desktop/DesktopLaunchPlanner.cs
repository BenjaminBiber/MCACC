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
    public static ProcessSpec PlanWindows(DesktopInstallation inst, AccountPaths paths, string homeDir)
    {
        string[] arguments = paths.DesktopUserDataDir is { } userDataDir ? [UserDataDirArgument(userDataDir)] : [];
        return paths.ClaudeConfigDir is { } configDir
            ? new ProcessSpec(inst.LaunchPath, arguments, homeDir, new Dictionary<string, string> { [ConfigDir] = configDir }, EnvironmentScrub.Always)
            : new ProcessSpec(inst.LaunchPath, arguments, homeDir, ReadOnlyDictionary<string, string>.Empty, ScrubWithConfigDir);
    }

    public static ProcessSpec PlanMac(DesktopInstallation inst, AccountPaths paths)
    {
        List<string> arguments = ["-n", "-a", inst.LaunchPath];
        if (paths.ClaudeConfigDir is { } configDir)
        {
            arguments.AddRange(["--env", $"{ConfigDir}={configDir}"]);
        }

        if (paths.DesktopUserDataDir is { } userDataDir)
        {
            arguments.AddRange(["--args", UserDataDirArgument(userDataDir)]);
        }

        return new ProcessSpec(Open, arguments, WorkingDirectory: null, ReadOnlyDictionary<string, string>.Empty, ScrubWithConfigDir);
    }

    private static string UserDataDirArgument(string userDataDir) => $"{ClaudeDesktopConstants.UserDataDirSwitch}={userDataDir}";
}
