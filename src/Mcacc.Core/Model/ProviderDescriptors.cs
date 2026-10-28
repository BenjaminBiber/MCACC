using Mcacc.Core.ConfigCopy;

namespace Mcacc.Core.Model;

/// <summary>The providers MCACC knows, one descriptor per <see cref="ProviderKind"/>.</summary>
public static class ProviderDescriptors
{
    public static ProviderDescriptor Claude { get; } = new(
        ProviderKind.Claude,
        DisplayName: "Claude",
        CliDisplayName: "Claude Code",
        DesktopDisplayName: "Claude Desktop",
        ProfileSubfolder: "claude",
        DesktopSubfolder: "desktop",
        new ProviderCli("claude", "CLAUDE_CONFIG_DIR", ".claude"),
        new ProviderDesktop(
            MsixPackageFamilyName: "Claude_pzs8sxrjxfjjc",
            MacBundleId: "com.anthropic.claudefordesktop",
            MacBundleName: "Claude.app",
            DeepLinkScheme: "claude://",
            DataFolderName: "Claude",
            WindowsPathDiscriminator: @"\WindowsApps\Claude_",
            WindowsProcessName: "Claude.exe"),
        new ProviderDefaults(Account.DefaultId, Account.DefaultName, Account.DefaultColor),
        [
            CopyItem.ClaudeMd,
            CopyItem.SettingsJson,
            CopyItem.Agents,
            CopyItem.Skills,
            CopyItem.Commands,
            CopyItem.Scripts,
            CopyItem.DesktopMcpConfig,
        ]);

    /// <remarks>
    /// The desktop app is called "ChatGPT" in the start menu but ships as the Codex package, and the separate
    /// ChatGPT app (<c>com.openai.chat</c>) carries the same executable name — hence the package discriminator.
    /// </remarks>
    public static ProviderDescriptor Codex { get; } = new(
        ProviderKind.Codex,
        DisplayName: "Codex",
        CliDisplayName: "Codex",
        DesktopDisplayName: "ChatGPT",
        ProfileSubfolder: "codex",
        DesktopSubfolder: "codex-desktop",
        new ProviderCli("codex", "CODEX_HOME", ".codex"),
        new ProviderDesktop(
            MsixPackageFamilyName: "OpenAI.Codex_2p2nqsd0c76g0",
            MacBundleId: "com.openai.codex",
            MacBundleName: "ChatGPT.app",
            DeepLinkScheme: "codex://",
            DataFolderName: "Codex",
            WindowsPathDiscriminator: @"\WindowsApps\OpenAI.Codex_",
            WindowsProcessName: "ChatGPT.exe",
            UserDataDirVariable: "CODEX_ELECTRON_USER_DATA_PATH"),
        new ProviderDefaults("codex", "Codex", "#5E8C8A"),
        // Deliberately not copyable: plugins, packages, vendor_imports and cache - machine-specific paths and
        // hundreds of megabytes of binaries.
        [CopyItem.CodexConfigToml, CopyItem.AgentsMd, CopyItem.CodexSkills, CopyItem.CodexPrompts]);

    public static IReadOnlyList<ProviderDescriptor> All { get; } = [Claude, Codex];

    public static ProviderDescriptor For(ProviderKind kind) => kind switch
    {
        ProviderKind.Claude => Claude,
        ProviderKind.Codex => Codex,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static ProviderDescriptor For(Account account) => For(account.Provider);
}
