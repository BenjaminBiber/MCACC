namespace Mcacc.Core.Tests.TestSupport;

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("mcacc-tests-").FullName;

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
