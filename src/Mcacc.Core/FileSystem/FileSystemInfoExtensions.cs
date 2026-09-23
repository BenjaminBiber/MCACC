namespace Mcacc.Core.FileSystem;

internal static class FileSystemInfoExtensions
{
    private static readonly EnumerationOptions AllChildren = new() { AttributesToSkip = 0, IgnoreInaccessible = false };

    /// <summary>Also lists hidden and system entries, and does not swallow access errors.</summary>
    public static IEnumerable<FileSystemInfo> EnumerateAllChildren(this DirectoryInfo directory) =>
        directory.EnumerateFileSystemInfos("*", AllChildren);

    /// <summary>Symlinks, junctions and other reparse points, which copying and deleting never follow.</summary>
    public static bool IsReparsePoint(this FileSystemInfo entry) =>
        entry.LinkTarget is not null || (entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint));
}
