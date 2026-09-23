namespace Mcacc.Core.ConfigCopy;

public static class CopyItems
{
    public static (string RelativePath, bool IsDirectory, CopyLocation Location) Info(CopyItem item) => item switch
    {
        CopyItem.ClaudeMd => ("CLAUDE.md", false, CopyLocation.ClaudeDir),
        CopyItem.SettingsJson => ("settings.json", false, CopyLocation.ClaudeDir),
        CopyItem.Agents => ("agents", true, CopyLocation.ClaudeDir),
        CopyItem.Skills => ("skills", true, CopyLocation.ClaudeDir),
        CopyItem.Commands => ("commands", true, CopyLocation.ClaudeDir),
        CopyItem.Scripts => ("scripts", true, CopyLocation.ClaudeDir),
        CopyItem.DesktopMcpConfig => ("claude_desktop_config.json", false, CopyLocation.DesktopDir),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };
}
