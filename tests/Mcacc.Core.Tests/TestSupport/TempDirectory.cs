namespace Mcacc.Core.Tests.TestSupport;

internal sealed class TempDirectory : IDisposable
{
    private static readonly EnumerationOptions AllEntries = new() { AttributesToSkip = 0 };

    public string Path { get; } = Directory.CreateTempSubdirectory("mcacc-tests-").FullName;

    public void Dispose() => DeleteTree(new DirectoryInfo(Path));

    // Tests leave read-only entries and junctions behind on purpose. Windows refuses to delete read-only
    // entries, and Directory.Delete(recursive) cannot remove a nested junction without admin rights.
    private static void DeleteTree(DirectoryInfo directory)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos("*", AllEntries).ToList())
        {
            if (entry.LinkTarget is not null)
            {
                entry.Delete();
            }
            else if (entry is DirectoryInfo subdirectory)
            {
                DeleteTree(subdirectory);
            }
            else
            {
                entry.Attributes &= ~FileAttributes.ReadOnly;
                entry.Delete();
            }
        }

        directory.Attributes &= ~FileAttributes.ReadOnly;
        directory.Delete();
    }
}
