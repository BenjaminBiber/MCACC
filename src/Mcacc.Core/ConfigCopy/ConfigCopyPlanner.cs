using Mcacc.Core.Paths;

namespace Mcacc.Core.ConfigCopy;

public static class ConfigCopyPlanner
{
    public static IReadOnlySet<CopyItem> Available(AccountPaths source)
    {
        var desktopDir = SourceDesktopDir(source);
        return Enum.GetValues<CopyItem>().Where(item => SourcePathIfPresent(item, source, desktopDir) is not null).ToHashSet();
    }

    /// <summary>Plans the requested items that are available, in <see cref="CopyItem"/> order.</summary>
    public static CopyPlan Plan(AccountPaths source, AccountPaths target, CopyRequest req)
    {
        if (target.ClaudeConfigDir is not { } targetClaudeDir || target.DesktopUserDataDir is not { } targetDesktopDir)
        {
            throw new ArgumentException("The default account is never a copy target.", nameof(target));
        }

        var sourceDesktopDir = SourceDesktopDir(source);
        var operations = new List<CopyOperation>();
        foreach (var item in Enum.GetValues<CopyItem>())
        {
            if (!req.Items.Contains(item) || SourcePathIfPresent(item, source, sourceDesktopDir) is not { } sourcePath)
            {
                continue;
            }

            var (relativePath, isDirectory, location) = CopyItems.Info(item);
            var targetDir = location == CopyLocation.ClaudeDir ? targetClaudeDir : targetDesktopDir;
            operations.Add(new CopyOperation(
                item,
                sourcePath,
                Path.Join(targetDir, relativePath),
                isDirectory,
                RewritePaths: item == CopyItem.SettingsJson && req.RewriteSettingsPaths));
        }

        return new CopyPlan(operations);
    }

    private static string? SourceDesktopDir(AccountPaths source) => source.DesktopDataDirs.FirstOrDefault(Directory.Exists);

    private static string? SourcePathIfPresent(CopyItem item, AccountPaths source, string? desktopDir)
    {
        var (relativePath, isDirectory, location) = CopyItems.Info(item);
        var dir = location == CopyLocation.ClaudeDir ? source.EffectiveClaudeDir : desktopDir;
        if (dir is null)
        {
            return null;
        }

        var path = Path.Join(dir, relativePath);
        var exists = isDirectory ? Directory.Exists(path) : File.Exists(path);
        return exists ? path : null;
    }
}
