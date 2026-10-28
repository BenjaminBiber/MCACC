using System.Collections.ObjectModel;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Desktop;

public static class DesktopLaunchPlanner
{
    private const string Open = "/usr/bin/open";

    /// <summary>Chromium's own switch; both desktop apps are built on it.</summary>
    public const string UserDataDirSwitch = "--user-data-dir";

    /// <param name="homeDir">The working directory of the desktop app.</param>
    /// <param name="deepLinkUrl">
    /// A <c>claude://</c> or <c>codex://</c> URL to hand to the profile. Electron holds its single-instance lock
    /// per user data directory, so a running instance of that profile receives the URL and this start exits again.
    /// </param>
    public static ProcessSpec PlanWindows(
        DesktopInstallation inst,
        AccountPaths paths,
        string homeDir,
        string? deepLinkUrl = null)
    {
        var env = Environment(paths);
        return new ProcessSpec(
            inst.LaunchPath,
            Arguments(paths, deepLinkUrl),
            homeDir,
            env,
            env.Count == 0 ? EnvironmentScrub.WithoutAnyConfigDir : EnvironmentScrub.For(paths.Provider));
    }

    /// <param name="deepLinkUrl">See <see cref="PlanWindows"/>.</param>
    public static ProcessSpec PlanMac(DesktopInstallation inst, AccountPaths paths, string? deepLinkUrl = null)
    {
        // open starts the bundle, so the profile's variables travel as --env instead of on this process.
        List<string> arguments = ["-n", "-a", inst.LaunchPath];
        foreach (var (name, value) in Environment(paths))
        {
            arguments.AddRange(["--env", $"{name}={value}"]);
        }

        var appArguments = Arguments(paths, deepLinkUrl);
        if (appArguments.Count > 0)
        {
            arguments.Add("--args");
            arguments.AddRange(appArguments);
        }

        return new ProcessSpec(
            Open,
            arguments,
            WorkingDirectory: null,
            ReadOnlyDictionary<string, string>.Empty,
            EnvironmentScrub.WithoutAnyConfigDir);
    }

    /// <summary>
    /// What the app needs to find the profile. The Codex app wants its Electron directory named in the environment
    /// as well as on the command line; Claude Desktop takes the switch alone.
    /// </summary>
    private static Dictionary<string, string> Environment(AccountPaths paths)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (paths.ConfigDirOverride is { } configDir)
        {
            env[paths.Provider.Cli.ConfigDirVariable] = configDir;
        }

        if (paths.Provider.Desktop?.UserDataDirVariable is { } variable && paths.DesktopUserDataDir is { } dataDir)
        {
            env[variable] = dataDir;
        }

        return env;
    }

    /// <summary>The switch comes first, so Chromium has the profile before it looks at the URL behind it.</summary>
    private static List<string> Arguments(AccountPaths paths, string? deepLinkUrl)
    {
        List<string> arguments = [];
        if (paths.DesktopUserDataDir is { } userDataDir)
        {
            arguments.Add($"{UserDataDirSwitch}={userDataDir}");
        }

        if (deepLinkUrl is not null)
        {
            arguments.Add(deepLinkUrl);
        }

        return arguments;
    }
}
