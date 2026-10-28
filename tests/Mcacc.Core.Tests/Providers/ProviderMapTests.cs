using Mcacc.Core.Model;
using Mcacc.Core.Providers;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Providers;

public class ProviderMapTests
{
    private static Account AccountOf(ProviderKind provider) =>
        new("privat", "Privat", "#6A9BCC", IsDefault: false, provider);

    [Fact]
    public void Resolves_the_entry_of_the_accounts_provider()
    {
        var map = new ProviderMap<string>(new Dictionary<ProviderKind, string>
        {
            [ProviderKind.Claude] = "claude",
            [ProviderKind.Codex] = "codex",
        });

        map.For(AccountOf(ProviderKind.Claude)).ShouldBe("claude");
        map.For(AccountOf(ProviderKind.Codex)).ShouldBe("codex");
    }

    /// <summary>
    /// Falling back to another provider's implementation would read the wrong files or start the wrong program, so a
    /// gap has to be loud.
    /// </summary>
    [Fact]
    public void A_provider_without_an_entry_throws_instead_of_falling_back()
    {
        var map = new ProviderMap<string>(new Dictionary<ProviderKind, string> { [ProviderKind.Claude] = "claude" });

        var e = Should.Throw<NotSupportedException>(() => map.For(AccountOf(ProviderKind.Codex)));

        e.Message.ShouldContain("Codex");
    }

    [Fact]
    public void An_unknown_provider_kind_throws_too()
    {
        var map = new ProviderMap<string>(new Dictionary<ProviderKind, string> { [ProviderKind.Claude] = "claude" });

        Should.Throw<NotSupportedException>(() => map.For(AccountOf((ProviderKind)42)));
    }

    [Fact]
    public void Rejects_a_map_that_is_empty()
    {
        Should.Throw<ArgumentException>(() => new ProviderMap<string>(new Dictionary<ProviderKind, string>()));
    }
}
