using Mcacc.Core.Paths;

namespace Mcacc.Core.ConfigCopy;

public static class ConfigCopyPlanner
{
    /// <summary>
    /// Only the source provider's own items. Two providers keep different things under the same name — a
    /// <c>skills</c> directory means one thing in a Claude home and another in a Codex one.
    /// </summary>
    public static IReadOnlySet<CopyItem> Available(AccountPaths source)
    {
        var desktopDir = SourceDesktopDir(source);
        return source.Provider.CopyItems
            .Where(item => SourcePathIfPresent(item, source, desktopDir) is not null)
            .ToHashSet();
    }

    /// <summary>Plans the requested items that are available, in <see cref="CopyItem"/> order.</summary>
    /// <exception cref="ArgumentException">
    /// The target is the account that uses the provider's own home, or the two accounts have different providers.
    /// </exception>
    public static CopyPlan Plan(AccountPaths source, AccountPaths target, CopyRequest req)
    {
        if (target.ConfigDirOverride is not { } targetConfigDir)
        {
            throw new ArgumentException("The default account is never a copy target.", nameof(target));
        }

        if (target.Provider.Kind != source.Provider.Kind)
        {
            throw new ArgumentException(
                $"A {source.Provider.DisplayName} account is no copy source for a {target.Provider.DisplayName} one.",
                nameof(target));
        }

        var sourceDesktopDir = SourceDesktopDir(source);
        var operations = new List<CopyOperation>();
        foreach (var item in source.Provider.CopyItems)
        {
            if (!req.Items.Contains(item) || SourcePathIfPresent(item, source, sourceDesktopDir) is not { } sourcePath)
            {
                continue;
            }

            var entry = CopyItems.Info(item);
            var targetDir = entry.Location == CopyLocation.ConfigDir ? targetConfigDir : target.DesktopUserDataDir;
            if (targetDir is null)
            {
                continue;
            }

            operations.Add(new CopyOperation(
                item,
                sourcePath,
                Path.Join(targetDir, entry.RelativePath),
                entry.IsDirectory,
                RewritePaths: Rewrites(entry, req)));
        }

        return new CopyPlan(operations);
    }

    /// <summary>
    /// A required rewrite happens whatever the user checked: that file names the config-home variable itself, so an
    /// unrewritten copy would quietly point the new profile back at the old one. Only the optional one is a choice.
    /// </summary>
    private static bool Rewrites(CopyItems.Entry entry, CopyRequest req) => entry.Rewrite switch
    {
        PathRewrite.Required => true,
        PathRewrite.Optional => req.RewriteSettingsPaths,
        _ => false,
    };

    private static string? SourceDesktopDir(AccountPaths source) => source.DesktopDataDirs.FirstOrDefault(Directory.Exists);

    private static string? SourcePathIfPresent(CopyItem item, AccountPaths source, string? desktopDir)
    {
        var entry = CopyItems.Info(item);
        var dir = entry.Location == CopyLocation.ConfigDir ? source.EffectiveConfigDir : desktopDir;
        if (dir is null)
        {
            return null;
        }

        var path = Path.Join(dir, entry.RelativePath);
        var exists = entry.IsDirectory ? Directory.Exists(path) : File.Exists(path);
        return exists ? path : null;
    }
}
