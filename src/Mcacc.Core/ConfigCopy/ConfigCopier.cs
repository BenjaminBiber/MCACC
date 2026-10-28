using Mcacc.Core.FileSystem;
using Mcacc.Core.Paths;

namespace Mcacc.Core.ConfigCopy;

public sealed class ConfigCopier
{
    public CopyReport Execute(CopyPlan plan, AccountPaths source, AccountPaths target)
    {
        if (target.ConfigDirOverride is null)
        {
            throw new ArgumentException("The default account is never a copy target.", nameof(target));
        }

        if (target.Provider.Kind != source.Provider.Kind)
        {
            throw new ArgumentException(
                $"A {source.Provider.DisplayName} account is no copy source for a {target.Provider.DisplayName} one.",
                nameof(target));
        }

        // Checked up front, so a bogus plan copies nothing at all.
        foreach (var operation in plan.Operations)
        {
            if (!MatchesWhitelist(operation, source, target))
            {
                throw new ArgumentException($"The operation does not match the copy whitelist: {operation}", nameof(plan));
            }
        }

        var copied = new List<CopyItem>();
        var skipped = new List<SkippedEntry>();
        foreach (var operation in plan.Operations)
        {
            var relativePath = CopyItems.Info(operation.Item).RelativePath;
            FileSystemInfo sourceEntry = operation.IsDirectory
                ? new DirectoryInfo(operation.SourcePath)
                : new FileInfo(operation.SourcePath);
            if (SkipReason(sourceEntry, operation.TargetPath) is { } reason)
            {
                skipped.Add(new SkippedEntry(relativePath, reason));
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(operation.TargetPath)!);
            if (sourceEntry is DirectoryInfo directory)
            {
                CopyDirectory(directory, operation.TargetPath, relativePath, skipped);
            }
            else if (operation.RewritePaths)
            {
                CopyRewritten(operation, source, target);
            }
            else
            {
                File.Copy(operation.SourcePath, operation.TargetPath, overwrite: false);
            }

            copied.Add(operation.Item);
        }

        return new CopyReport(copied, skipped);
    }

    /// <summary>
    /// The operation must be exactly what the planner derives for its item: the whitelisted path inside the source's
    /// config dir or one of its Desktop candidates, copied to the same path inside the target profile.
    /// </summary>
    private static bool MatchesWhitelist(CopyOperation operation, AccountPaths source, AccountPaths target)
    {
        var entry = CopyItems.Info(operation.Item);
        IReadOnlyList<string> sourceDirs = entry.Location == CopyLocation.ConfigDir
            ? [source.EffectiveConfigDir]
            : source.DesktopDataDirs;
        var targetDir = entry.Location == CopyLocation.ConfigDir
            ? target.ConfigDirOverride
            : target.DesktopUserDataDir;
        return operation.IsDirectory == entry.IsDirectory
            && source.Provider.CopyItems.Contains(operation.Item)
            && (!operation.RewritePaths || entry.Rewrite != PathRewrite.None)
            && targetDir is not null
            && SamePath(operation.TargetPath, Path.Join(targetDir, entry.RelativePath))
            && sourceDirs.Any(dir => SamePath(operation.SourcePath, Path.Join(dir, entry.RelativePath)));
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.Ordinal);

    private static CopySkipReason? SkipReason(FileSystemInfo source, string targetPath)
    {
        if (source.IsReparsePoint())
        {
            return CopySkipReason.ReparsePoint;
        }

        if (!source.Exists)
        {
            return CopySkipReason.SourceMissing;
        }

        if (source is FileInfo)
        {
            return File.Exists(targetPath) || Directory.Exists(targetPath) ? CopySkipReason.TargetExists : null;
        }

        if (File.Exists(targetPath))
        {
            return CopySkipReason.TargetExists;
        }

        // An existing real directory is merged; a linked one would be written through.
        return new DirectoryInfo(targetPath).IsReparsePoint() ? CopySkipReason.ReparsePoint : null;
    }

    private static void CopyDirectory(DirectoryInfo source, string targetPath, string relativePath, List<SkippedEntry> skipped)
    {
        Directory.CreateDirectory(targetPath);
        foreach (var entry in source.EnumerateAllChildren())
        {
            var entryRelativePath = Path.Join(relativePath, entry.Name);
            var entryTargetPath = Path.Join(targetPath, entry.Name);
            if (SkipReason(entry, entryTargetPath) is { } reason)
            {
                skipped.Add(new SkippedEntry(entryRelativePath, reason));
            }
            else if (entry is DirectoryInfo directory)
            {
                CopyDirectory(directory, entryTargetPath, entryRelativePath, skipped);
            }
            else
            {
                File.Copy(entry.FullName, entryTargetPath, overwrite: false);
            }
        }
    }

    private static void CopyRewritten(CopyOperation operation, AccountPaths source, AccountPaths target)
    {
        var rewritten = ProfilePathRewriter.Rewrite(File.ReadAllText(operation.SourcePath), source, target);
        using var stream = new FileStream(operation.TargetPath, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream);
        writer.Write(rewritten);
    }
}
