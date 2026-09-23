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
    DesktopProcessRules Rules,
    IProcessRunner Runner,
    IProcessSnapshotProvider Snapshots,
    IDesktopInstallationLocator InstallationLocator,
    IDesktopInstanceTerminator Terminator,
    ITerminalCatalog Terminals,
    IExecutableLocator Executables,
    IShellIntegration Shell,
    ICredentialSource Credentials,
    IUsageHttp UsageHttp)
{
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
                DesktopProcessRules.ForOs(info.Os),
                runner,
                new WmiProcessSnapshotProvider(),
                new WindowsDesktopInstallationLocator(info),
                new WindowsDesktopInstanceTerminator(),
                new WindowsTerminalCatalog(info, executables),
                executables,
                new WindowsShellIntegration(runner),
                new FileCredentialSource(),
                new UsageHttpClient());
        }

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException($"MCACC supports Windows and macOS, not {info.Os}.");
        }

        return new PlatformServices(
            info,
            paths,
            DesktopProcessRules.ForOs(info.Os),
            runner,
            new PsProcessSnapshotProvider(runner),
            new MacDesktopInstallationLocator(info, runner),
            new MacDesktopInstanceTerminator(runner),
            new MacTerminalCatalog(info, runner),
            executables,
            new MacShellIntegration(runner),
            new MacKeychainCredentialSource(runner),
            new UsageHttpClient());
    }

    public AccountPathResolver CreateResolver() => new(Paths, Info);

    public IDesktopInstanceDetector CreateDetector() => new DesktopInstanceDetector(Snapshots, Rules);
}
