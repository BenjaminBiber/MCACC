using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Terminal;

namespace Mcacc.Platform.Windows;

/// <summary>The display names are proper names of German Windows, so they stay here instead of in the UI texts.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsTerminalCatalog(PlatformInfo info, IExecutableLocator locator, Func<string, string?>? getEnv = null)
    : ITerminalCatalog
{
    private readonly Func<string, string?> _getEnv = getEnv ?? Environment.GetEnvironmentVariable;
    private IReadOnlyList<TerminalOption>? _installed;

    public string PlatformDefaultId =>
        GetInstalled().Any(option => option.Id == TerminalIds.WindowsTerminal)
            ? TerminalIds.WindowsTerminal
            : TerminalIds.WindowsPowerShell;

    /// <remarks>Scanned once; an install or uninstall during the session is not picked up.</remarks>
    public IReadOnlyList<TerminalOption> GetInstalled() => _installed ??= Scan();

    private IReadOnlyList<TerminalOption> Scan()
    {
        var style = info.PathStyle;
        var options = new List<TerminalOption>();
        Add(options, TerminalIds.WindowsTerminal, "Windows Terminal", [
            locator.Find("wt.exe"),
            style.Join(info.LocalAppData, "Microsoft", "WindowsApps", "wt.exe"),
        ]);
        Add(options, TerminalIds.Pwsh, "PowerShell 7", [
            locator.Find("pwsh.exe"),
            style.Join(_getEnv("ProgramFiles") ?? @"C:\Program Files", "PowerShell", "7", "pwsh.exe"),
        ]);
        Add(options, TerminalIds.WindowsPowerShell, "Windows PowerShell", [
            style.Join(SystemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
        ]);
        Add(options, TerminalIds.Cmd, "Eingabeaufforderung", [
            _getEnv("ComSpec"),
            style.Join(SystemRoot, "System32", "cmd.exe"),
        ]);
        return options;
    }

    private string SystemRoot => _getEnv("SystemRoot") ?? @"C:\WINDOWS";

    private static void Add(List<TerminalOption> options, string id, string displayName, IReadOnlyList<string?> candidates)
    {
        if (First(candidates) is { } path)
        {
            options.Add(new TerminalOption(id, displayName, path));
        }
    }

    private static string? First(IReadOnlyList<string?> candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrEmpty(candidate) && File.Exists(candidate));
}
