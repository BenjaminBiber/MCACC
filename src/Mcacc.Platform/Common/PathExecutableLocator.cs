using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Platform.Common;

/// <summary>
/// Finds an executable the way a shell would, plus the places a login shell adds to PATH but MCACC does not inherit.
/// </summary>
public sealed class PathExecutableLocator(PlatformInfo info, Func<string, string?>? getEnv = null) : IExecutableLocator
{
    private static readonly string[] WindowsFallbackExtensions = [".exe", ".cmd", ".bat", ".com"];

    private readonly Func<string, string?> _getEnv = getEnv ?? Environment.GetEnvironmentVariable;

    public string? Find(string name)
    {
        foreach (var directory in Directories())
        {
            foreach (var candidate in Candidates(name))
            {
                var path = Join(directory, candidate);
                if (path is not null && IsExecutableFile(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private IEnumerable<string> Directories()
    {
        var separator = info.Os == OsKind.Windows ? ';' : ':';
        var path = _getEnv("PATH") ?? "";
        return path.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(ExtraDirectories());
    }

    private IEnumerable<string> ExtraDirectories()
    {
        var style = info.PathStyle;
        return info.Os switch
        {
            OsKind.Windows =>
            [
                style.Join(info.LocalAppData, "Microsoft", "WinGet", "Links"),
                style.Join(info.LocalAppData, "Microsoft", "WindowsApps"),
                style.Join(info.HomeDir, ".local", "bin"),
                style.Join(info.LocalAppData, "Programs", "OpenAI", "Codex", "bin"),
                style.Join(info.HomeDir, ".codex", "packages", "standalone", "current", "bin"),
            ],
            OsKind.MacOS =>
            [
                "/opt/homebrew/bin",
                "/usr/local/bin",
                style.Join(info.HomeDir, ".local", "bin"),
                style.Join(info.HomeDir, ".claude", "local"),
                style.Join(info.HomeDir, ".codex", "bin"),
                style.Join(info.HomeDir, ".codex", "packages", "standalone", "current", "bin"),
            ],
            _ => [style.Join(info.HomeDir, ".local", "bin")],
        };
    }

    private IEnumerable<string> Candidates(string name)
    {
        if (info.Os != OsKind.Windows || Path.HasExtension(name))
        {
            return [name];
        }

        var extensions = _getEnv("PATHEXT")?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (extensions is { Length: > 0 } ? extensions : WindowsFallbackExtensions).Select(extension => name + extension);
    }

    private static string? Join(string directory, string name)
    {
        try
        {
            return Path.Join(directory, name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsExecutableFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(path) & anyExecute) != UnixFileMode.None;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
