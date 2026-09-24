using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public sealed class ProfileCacheTests : IDisposable
{
    private static readonly AccountProfile Profile = new(
        "abc", "privat@example.com", "Max Muster", "Max", true, false,
        new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), "Privat", "claude_max", "stripe_subscription", "default_claude_max_5x");

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private McaccPaths Paths => new(
        Path.Join(_temp.Path, ".mcacc"),
        OperatingSystem.IsWindows() ? PathStyle.Windows : PathStyle.Posix);

    [Fact]
    public void Saves_and_loads_the_profiles()
    {
        new ProfileCache(Paths).Save(new Dictionary<string, AccountProfile> { ["privat"] = Profile });

        new ProfileCache(Paths).Load()["privat"].ShouldBe(Profile);
    }

    [Fact]
    public void Without_a_file_there_are_no_profiles()
    {
        new ProfileCache(Paths).Load().ShouldBeEmpty();
    }
}
