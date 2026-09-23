namespace Mcacc.Core.ConfigCopy;

/// <summary>The whitelist of copyable items; anything else in a Claude or Desktop directory is never copied.</summary>
public enum CopyItem
{
    ClaudeMd,
    SettingsJson,
    Agents,
    Skills,
    Commands,
    Scripts,
    DesktopMcpConfig,
}
