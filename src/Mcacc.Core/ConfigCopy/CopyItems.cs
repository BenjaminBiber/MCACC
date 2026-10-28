namespace Mcacc.Core.ConfigCopy;

public static class CopyItems
{
    public sealed record Entry(
        string RelativePath,
        bool IsDirectory,
        CopyLocation Location,
        PathRewrite Rewrite = PathRewrite.None);

    public static Entry Info(CopyItem item) => item switch
    {
        CopyItem.ClaudeMd => new("CLAUDE.md", false, CopyLocation.ConfigDir),
        CopyItem.SettingsJson => new("settings.json", false, CopyLocation.ConfigDir, PathRewrite.Optional),
        CopyItem.Agents => new("agents", true, CopyLocation.ConfigDir),
        CopyItem.Skills => new("skills", true, CopyLocation.ConfigDir),
        CopyItem.Commands => new("commands", true, CopyLocation.ConfigDir),
        CopyItem.Scripts => new("scripts", true, CopyLocation.ConfigDir),
        CopyItem.DesktopMcpConfig => new("claude_desktop_config.json", false, CopyLocation.DesktopDir),

        // config.toml sets CODEX_HOME itself, so copying it unchanged would de-isolate the new profile.
        CopyItem.CodexConfigToml => new("config.toml", false, CopyLocation.ConfigDir, PathRewrite.Required),
        CopyItem.AgentsMd => new("AGENTS.md", false, CopyLocation.ConfigDir),
        CopyItem.CodexSkills => new("skills", true, CopyLocation.ConfigDir),
        CopyItem.CodexPrompts => new("prompts", true, CopyLocation.ConfigDir),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };
}
