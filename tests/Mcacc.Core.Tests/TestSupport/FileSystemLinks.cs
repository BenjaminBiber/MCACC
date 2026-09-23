using System.Diagnostics;
using Xunit;

namespace Mcacc.Core.Tests.TestSupport;

internal static class FileSystemLinks
{
    /// <summary>A junction on Windows, which needs no symlink privilege; a symlink elsewhere.</summary>
    public static void CreateDirectoryLink(string link, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            CreateJunction(link, target);
        }
        else
        {
            Directory.CreateSymbolicLink(link, target);
        }
    }

    public static void CreateFileLink(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception e) when (OperatingSystem.IsWindows() && e is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("File symlinks need Developer Mode or admin rights on Windows.");
        }
    }

    private static void CreateJunction(string link, string target)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("cmd.exe did not start.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"mklink /J failed: {output}");
        }
    }
}
