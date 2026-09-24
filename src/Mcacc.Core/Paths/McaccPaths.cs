namespace Mcacc.Core.Paths;

public sealed class McaccPaths
{
    private const string HomeVariable = "MCACC_HOME";

    public McaccPaths(string root, PathStyle style)
    {
        Root = root;
        Style = style;
        ConfigFile = style.Join(root, "config.json");
        ConfigBackupFile = style.Join(root, "config.json.bak");
        UsageCacheFile = style.Join(root, "usage-cache.json");
        ProfilesDir = style.Join(root, "profiles");
        LaunchDir = style.Join(root, "launch");
        LogsDir = style.Join(root, "logs");
        ProfileCacheFile = style.Join(root, "profile-cache.json");
        UsageHistoryDir = style.Join(root, "usage-history");
        StatsDir = style.Join(root, "stats");
    }

    public string Root { get; }

    public PathStyle Style { get; }

    public string ConfigFile { get; }

    public string ConfigBackupFile { get; }

    public string UsageCacheFile { get; }

    public string ProfilesDir { get; }

    public string LaunchDir { get; }

    public string LogsDir { get; }

    public string ProfileCacheFile { get; }

    public string UsageHistoryDir { get; }

    public string StatsDir { get; }

    public static McaccPaths FromEnvironment(PlatformInfo info, Func<string, string?> getEnv)
    {
        var overridden = getEnv(HomeVariable);
        var root = string.IsNullOrWhiteSpace(overridden)
            ? info.PathStyle.Join(info.HomeDir, ".mcacc")
            : overridden;
        return new McaccPaths(root, info.PathStyle);
    }

    public string ProfileDir(string id) => Style.Join(ProfilesDir, id);

    public string DesktopDataDir(string id) => Style.Join(ProfileDir(id), "desktop");

    public string ClaudeConfigDir(string id) => Style.Join(ProfileDir(id), "claude");

    public string UsageHistoryFile(string id) => Style.Join(UsageHistoryDir, id + ".jsonl");

    public string StatsFile(string id) => Style.Join(StatsDir, id + ".json");
}
