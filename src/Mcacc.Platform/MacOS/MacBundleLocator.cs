using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;

namespace Mcacc.Platform.MacOS;

/// <summary>Finds an <c>.app</c> bundle in the usual folders, and asks Spotlight only if that fails.</summary>
[SupportedOSPlatform("macos")]
internal static class MacBundleLocator
{
    private const string MdFind = "/usr/bin/mdfind";

    private static readonly TimeSpan MdFindTimeout = TimeSpan.FromSeconds(5);

    public static string? Find(PlatformInfo info, IProcessRunner runner, string bundleName, string bundleId)
    {
        string[] candidates =
        [
            PathStyle.Posix.Join("/Applications", bundleName),
            PathStyle.Posix.Join(info.HomeDir, "Applications", bundleName),
        ];
        return candidates.FirstOrDefault(Directory.Exists) ?? Spotlight(runner, bundleId);
    }

    /// <remarks>
    /// Blocking is deliberate: the locator interfaces are synchronous, and this path is only taken when the bundle
    /// sits outside both Applications folders. Task.Run keeps it off the caller's synchronization context.
    /// </remarks>
    private static string? Spotlight(IProcessRunner runner, string bundleId)
    {
        var spec = new ProcessSpec(
            MdFind,
            [$"kMDItemCFBundleIdentifier == '{bundleId}'"],
            WorkingDirectory: null,
            ReadOnlyDictionary<string, string>.Empty,
            EnvironmentScrub.Always);
        try
        {
            var result = Task.Run(() => runner.RunAsync(spec, MdFindTimeout, CancellationToken.None)).GetAwaiter().GetResult();
            if (result.TimedOut || result.ExitCode != 0)
            {
                return null;
            }

            return result.StdOut
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(Directory.Exists);
        }
        catch (LaunchException)
        {
            return null;
        }
    }
}
