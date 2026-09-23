using Mcacc.Core.Terminal;

namespace Mcacc.Core.Tests.TestSupport;

/// <summary>Requests whose paths hold a space, an umlaut, both apostrophes, a percent sign, a semicolon and a dollar.</summary>
internal static class LaunchRequests
{
    public const string WindowsHome = @"C:\Users\Jö O'Neil’s 100%;$x";
    public const string WindowsConfigDir = WindowsHome + @"\.mcacc\profiles\privat\claude";
    public const string WindowsLaunchDir = WindowsHome + @"\.mcacc\launch";

    public const string PosixHome = "/Users/Jö O'Neil’s 100%;$x";
    public const string PosixConfigDir = PosixHome + "/.mcacc/profiles/privat/claude";
    public const string PosixLaunchDir = PosixHome + "/.mcacc/launch";

    public const string ProfileColor = "#6A9BCC";
    public const string DefaultColor = "#D97757";

    public static ClaudeCodeLaunchRequest WindowsProfile(string name = "Privat") =>
        new("privat", name, WindowsConfigDir, WindowsHome, WindowsLaunchDir, ProfileColor);

    public static ClaudeCodeLaunchRequest WindowsDefault() =>
        new("standard", "Standard", ClaudeConfigDir: null, WindowsHome, WindowsLaunchDir, DefaultColor);

    public static ClaudeCodeLaunchRequest PosixProfile(string name = "Privat") =>
        new("privat", name, PosixConfigDir, PosixHome, PosixLaunchDir, ProfileColor);

    public static ClaudeCodeLaunchRequest PosixDefault() =>
        new("standard", "Standard", ClaudeConfigDir: null, PosixHome, PosixLaunchDir, DefaultColor);
}
