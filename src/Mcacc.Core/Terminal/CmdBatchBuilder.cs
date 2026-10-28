using Mcacc.Core.Quoting;

namespace Mcacc.Core.Terminal;

public static class CmdBatchBuilder
{
    /// <exception cref="ArgumentException">The config dir or home contains a double quote or a line break.</exception>
    public static string Build(CliLaunchRequest req)
    {
        var configDir = req.ConfigDirOverride is null ? "" : BatchEscaping.ForSetValue(req.ConfigDirOverride);
        string[] lines =
        [
            "@chcp 65001 >nul",
            "@echo off",
            .. req.Scrub.Select(name => $"set \"{name}=\""),
            $"set \"{req.ConfigDirVariable}={configDir}\"",
            $"cd /d \"{BatchEscaping.ForSetValue(req.HomeDir)}\"",
            $"title {req.Title}",
            // call, so an npm .cmd shim returns here instead of ending this batch file.
            $"call {req.CliCommand}",
            // cmd /k keeps the window open; without this its prompt would stay hidden.
            "@echo on",
        ];
        return string.Concat(lines.Select(line => line + "\r\n"));
    }
}
