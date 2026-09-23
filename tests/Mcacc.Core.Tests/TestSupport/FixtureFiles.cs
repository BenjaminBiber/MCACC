namespace Mcacc.Core.Tests.TestSupport;

internal static class FixtureFiles
{
    public static string ReadAllText(string name) => File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Fixtures", name));
}
