using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Desktop;
using Mcacc.Core.Paths;
using Mcacc.Platform.Common;
using Mcacc.Platform.MacOS;
using Mcacc.Platform.Windows;

namespace Mcacc.Platform;

/// <summary>Everything that differs per operating system, picked once at startup.</summary>
public sealed record PlatformServices(
    PlatformInfo Info,
    McaccPaths Paths,
    IProcessRunner Runner,
    IProcessSnapshotProvider Snapshots,
    IDesktopInstallationLocator InstallationLocator,
    IDesktopInstanceTerminator Terminator,
    ITerminalCatalog Terminals,
    IExecutableLocator Executables,
    IShellIntegration Shell,
    ICredentialSource Credentials,
    IUsageHttp UsageHttp,
    ICodexUsageHttp CodexUsageHttp)
{
    /// <summary>The Dock icon on macOS; the other platforms have none.</summary>
    public IDockIcon Dock { get; init; } = NoDockIcon.Instance;

    /// <summary>The entry that starts MCACC at login; none where MCACC does not know how.</summary>
    public ILoginItem LoginItem { get; init; } = NoLoginItem.Instance;

    /// <exception cref="PlatformNotSupportedException">MCACC runs on Windows and macOS only.</exception>
    public static PlatformServices Create()
    {
        var info = PlatformInfo.Current();
        var paths = McaccPaths.FromEnvironment(info, Environment.GetEnvironmentVariable);
        var runner = new SystemProcessRunner();
        var executables = new PathExecutableLocator(info);
        if (OperatingSystem.IsWindows())
        {
            return new PlatformServices(
                info,
                paths,
                runner,
                new WmiProcessSnapshotProvider(),
                new WindowsDesktopInstallationLocator(info, runner),
                new WindowsDesktopInstanceTerminator(),
                new WindowsTerminalCatalog(info, executables),
                executables,
                new WindowsShellIntegration(runner),
                new FileCredentialSource(),
                new UsageHttpClient(),
                new CodexUsageHttpClient())
            {
                LoginItem = new WindowsLoginItem(),
            };
        }

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException($"MCACC supports Windows and macOS, not {info.Os}.");
        }

        return new PlatformServices(
            info,
            paths,
            runner,
            new PsProcessSnapshotProvider(runner),
            new MacDesktopInstallationLocator(info, runner),
            new MacDesktopInstanceTerminator(runner),
            new MacTerminalCatalog(info, runner),
            executables,
            new MacShellIntegration(runner),
            new MacKeychainCredentialSource(runner),
            new UsageHttpClient(),
            new CodexUsageHttpClient())
        {
            Dock = new MacDockIcon(),
            LoginItem = new MacLoginItem(Path.Join(info.HomeDir, "Library", "LaunchAgents")),
        };
    }

    public AccountPathResolver CreateResolver() => new(Paths, Info);

    public IDesktopInstanceDetector CreateDetector() => new DesktopInstanceDetector(Snapshots, Info);
}
