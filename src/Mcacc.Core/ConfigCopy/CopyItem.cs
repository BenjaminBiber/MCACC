namespace Mcacc.Core.ConfigCopy;

/// <summary>
/// The whitelist of copyable items; anything else in a provider's directory is never copied. Each item belongs to
/// exactly one provider — see <see cref="Mcacc.Core.Model.ProviderDescriptor.CopyItems"/>.
/// </summary>
public enum CopyItem
{
    ClaudeMd,
    SettingsJson,
    Agents,
    Skills,
    Commands,
    Scripts,
    DesktopMcpConfig,

    /// <summary>Codex: <c>config.toml</c>. Its paths are always rewritten; see <see cref="PathRewrite"/>.</summary>
    CodexConfigToml,

    /// <summary>Codex: <c>AGENTS.md</c>.</summary>
    AgentsMd,

    /// <summary>Codex: the <c>skills</c> directory.</summary>
    CodexSkills,

    /// <summary>Codex: the <c>prompts</c> directory.</summary>
    CodexPrompts,
}
