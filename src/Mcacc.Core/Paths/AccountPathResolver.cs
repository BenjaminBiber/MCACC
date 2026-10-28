using Mcacc.Core.Model;

namespace Mcacc.Core.Paths;

/// <summary>
/// Where an account's files are. One pair of methods per provider — the default account uses the provider's own
/// home directory, a profile account an isolated directory below <c>~/.mcacc/profiles</c>.
/// </summary>
public sealed class AccountPathResolver(McaccPaths paths, PlatformInfo info)
{
    private const string ClaudeJson = ".claude.json";
    private const string ClaudeCredentials = ".credentials.json";
    private const string CodexAuth = "auth.json";

    public AccountPaths Resolve(Account a)
    {
        var provider = ProviderDescriptors.For(a);
        return provider.Kind switch
        {
            ProviderKind.Claude => a.IsDefault ? ClaudeDefault(provider) : ClaudeProfile(a.Id, provider),
            ProviderKind.Codex => a.IsDefault ? CodexDefault(provider) : CodexProfile(a.Id, provider),
            _ => throw new ArgumentOutOfRangeException(nameof(a), a.Provider, null),
        };
    }

    /// <remarks>Claude Code keeps <c>.claude.json</c> beside its directory, not inside it.</remarks>
    private AccountPaths ClaudeDefault(ProviderDescriptor provider)
    {
        var style = info.PathStyle;
        var dir = style.Join(info.HomeDir, provider.Cli.DefaultHomeFolder);
        return new AccountPaths(
            provider,
            DesktopUserDataDir: null,
            ConfigDirOverride: null,
            EffectiveConfigDir: dir,
            IdentityFile: style.Join(info.HomeDir, ClaudeJson),
            CredentialsFile: style.Join(dir, ClaudeCredentials),
            DesktopDataDirs: DefaultDesktopDataDirs(provider));
    }

    private AccountPaths ClaudeProfile(string id, ProviderDescriptor provider)
    {
        var style = info.PathStyle;
        var desktopDir = paths.DesktopDataDir(id, provider);
        var dir = paths.ConfigDir(id, provider);
        return new AccountPaths(
            provider,
            DesktopUserDataDir: desktopDir,
            ConfigDirOverride: dir,
            EffectiveConfigDir: dir,
            IdentityFile: style.Join(dir, ClaudeJson),
            CredentialsFile: style.Join(dir, ClaudeCredentials),
            DesktopDataDirs: [desktopDir]);
    }

    /// <remarks>Codex keeps identity and token in one file, so both paths are the same.</remarks>
    private AccountPaths CodexDefault(ProviderDescriptor provider)
    {
        var style = info.PathStyle;
        var dir = style.Join(info.HomeDir, provider.Cli.DefaultHomeFolder);
        var auth = style.Join(dir, CodexAuth);
        return new AccountPaths(
            provider,
            DesktopUserDataDir: null,
            ConfigDirOverride: null,
            EffectiveConfigDir: dir,
            IdentityFile: auth,
            CredentialsFile: auth,
            DesktopDataDirs: DefaultDesktopDataDirs(provider));
    }

    private AccountPaths CodexProfile(string id, ProviderDescriptor provider)
    {
        var desktopDir = paths.DesktopDataDir(id, provider);
        var dir = paths.ConfigDir(id, provider);
        var auth = info.PathStyle.Join(dir, CodexAuth);
        return new AccountPaths(
            provider,
            DesktopUserDataDir: desktopDir,
            ConfigDirOverride: dir,
            EffectiveConfigDir: dir,
            IdentityFile: auth,
            CredentialsFile: auth,
            DesktopDataDirs: [desktopDir]);
    }

    /// <summary>Where the desktop app of the provider's own installation keeps its data.</summary>
    private IReadOnlyList<string> DefaultDesktopDataDirs(ProviderDescriptor provider)
    {
        if (provider.Desktop is not { } desktop)
        {
            return [];
        }

        var style = info.PathStyle;
        return info.Os switch
        {
            OsKind.Windows =>
            [
                style.Join(
                    info.LocalAppData,
                    "Packages",
                    desktop.MsixPackageFamilyName,
                    "LocalCache",
                    "Roaming",
                    desktop.DataFolderName),
                style.Join(info.RoamingAppData, desktop.DataFolderName),
            ],
            OsKind.MacOS => [style.Join(info.HomeDir, "Library", "Application Support", desktop.DataFolderName)],
            _ => [],
        };
    }
}
