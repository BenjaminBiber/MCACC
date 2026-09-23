using Mcacc.Core.Model;

namespace Mcacc.Core.Paths;

public sealed class AccountPathResolver(McaccPaths paths, PlatformInfo info)
{
    private const string DesktopPackageFamilyName = "Claude_pzs8sxrjxfjjc";

    public AccountPaths Resolve(Account a) => a.IsDefault ? ResolveDefault() : ResolveProfile(a.Id);

    private AccountPaths ResolveDefault()
    {
        var style = info.PathStyle;
        var claudeDir = style.Join(info.HomeDir, ".claude");
        return new AccountPaths(
            DesktopUserDataDir: null,
            ClaudeConfigDir: null,
            EffectiveClaudeDir: claudeDir,
            ClaudeJsonFile: style.Join(info.HomeDir, ".claude.json"),
            CredentialsFile: style.Join(claudeDir, ".credentials.json"),
            DesktopDataDirs: DefaultDesktopDataDirs());
    }

    private AccountPaths ResolveProfile(string id)
    {
        var style = info.PathStyle;
        var desktopDir = paths.DesktopDataDir(id);
        var claudeDir = paths.ClaudeConfigDir(id);
        return new AccountPaths(
            DesktopUserDataDir: desktopDir,
            ClaudeConfigDir: claudeDir,
            EffectiveClaudeDir: claudeDir,
            ClaudeJsonFile: style.Join(claudeDir, ".claude.json"),
            CredentialsFile: style.Join(claudeDir, ".credentials.json"),
            DesktopDataDirs: [desktopDir]);
    }

    private IReadOnlyList<string> DefaultDesktopDataDirs()
    {
        var style = info.PathStyle;
        return info.Os switch
        {
            OsKind.Windows =>
            [
                style.Join(info.LocalAppData, "Packages", DesktopPackageFamilyName, "LocalCache", "Roaming", "Claude"),
                style.Join(info.RoamingAppData, "Claude"),
            ],
            OsKind.MacOS => [style.Join(info.HomeDir, "Library", "Application Support", "Claude")],
            _ => [],
        };
    }
}
