using Mcacc.Core.Processes;
using Mcacc.Core.Quoting;

namespace Mcacc.Core.Terminal;

public static class CmdBatchBuilder
{
    /// <exception cref="ArgumentException">The config dir or home contains a double quote or a line break.</exception>
    public static string Build(ClaudeCodeLaunchRequest req)
    {
        var configDir = req.ClaudeConfigDir is null ? "" : BatchEscaping.ForSetValue(req.ClaudeConfigDir);
        string[] lines =
        [
            "@chcp 65001 >nul",
            "@echo off",
            .. EnvironmentScrub.Always.Select(name => $"set \"{name}=\""),
            $"set \"{EnvironmentScrub.ClaudeConfigDirVariable}={configDir}\"",
            $"cd /d \"{BatchEscaping.ForSetValue(req.HomeDir)}\"",
            $"title {req.Title}",
            // call, so an npm claude.cmd shim returns here instead of ending this batch file.
            "call claude",
            // cmd /k keeps the window open; without this its prompt would stay hidden.
            "@echo on",
        ];
        return string.Concat(lines.Select(line => line + "\r\n"));
    }
}
