using Mcacc.Core.Model;
using Mcacc.Core.Terminal;

namespace Mcacc.Core.Tests.TestSupport;

/// <summary>Requests whose paths hold a space, an umlaut, both apostrophes, a percent sign, a semicolon and a dollar.</summary>
internal static class LaunchRequests
{
    public const string WindowsHome = @"C:\Users\Jö O'Neil’s 100%;$x";
    public const string WindowsConfigDir = WindowsHome + @"\.mcacc\profiles\privat\claude";
    public const string WindowsCodexConfigDir = WindowsHome + @"\.mcacc\profiles\codex-privat\codex";
    public const string WindowsLaunchDir = WindowsHome + @"\.mcacc\launch";

    public const string PosixHome = "/Users/Jö O'Neil’s 100%;$x";
    public const string PosixConfigDir = PosixHome + "/.mcacc/profiles/privat/claude";
    public const string PosixCodexConfigDir = PosixHome + "/.mcacc/profiles/codex-privat/codex";
    public const string PosixLaunchDir = PosixHome + "/.mcacc/launch";

    public const string ProfileColor = "#6A9BCC";
    public const string DefaultColor = "#D97757";
    public const string CodexColor = "#5E8C8A";

    public static CliLaunchRequest WindowsProfile(string name = "Privat") =>
        new(ProviderDescriptors.Claude, "privat", name, WindowsConfigDir, WindowsHome, WindowsLaunchDir, ProfileColor);

    public static CliLaunchRequest WindowsDefault() =>
        new(
            ProviderDescriptors.Claude,
            "standard",
            "Standard",
            ConfigDirOverride: null,
            WindowsHome,
            WindowsLaunchDir,
            DefaultColor);

    public static CliLaunchRequest WindowsCodexProfile(string name = "Codex Privat") =>
        new(
            ProviderDescriptors.Codex,
            "codex-privat",
            name,
            WindowsCodexConfigDir,
            WindowsHome,
            WindowsLaunchDir,
            CodexColor);

    public static CliLaunchRequest PosixProfile(string name = "Privat") =>
        new(ProviderDescriptors.Claude, "privat", name, PosixConfigDir, PosixHome, PosixLaunchDir, ProfileColor);

    public static CliLaunchRequest PosixDefault() =>
        new(
            ProviderDescriptors.Claude,
            "standard",
            "Standard",
            ConfigDirOverride: null,
            PosixHome,
            PosixLaunchDir,
            DefaultColor);

    public static CliLaunchRequest PosixCodexProfile(string name = "Codex Privat") =>
        new(
            ProviderDescriptors.Codex,
            "codex-privat",
            name,
            PosixCodexConfigDir,
            PosixHome,
            PosixLaunchDir,
            CodexColor);

    /// <summary>The Codex account that uses <c>~/.codex</c>: the variable must be unset, not set to anything.</summary>
    public static CliLaunchRequest PosixCodexDefault() =>
        new(
            ProviderDescriptors.Codex,
            "codex",
            "Codex",
            ConfigDirOverride: null,
            PosixHome,
            PosixLaunchDir,
            CodexColor);
}
