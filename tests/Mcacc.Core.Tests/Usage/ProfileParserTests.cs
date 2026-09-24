using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public class ProfileParserTests
{
    [Fact]
    public void Reads_the_account_and_the_organization()
    {
        var profile = ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json"));

        profile.ShouldBe(new AccountProfile(
            "00000000-0000-0000-0000-000000000001",
            "max.muster@example.com",
            "Benjamin Biber",
            "Benjamin",
            HasClaudeMax: false,
            HasClaudePro: true,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "Example GmbH",
            "claude_pro",
            "stripe_subscription",
            "default_claude_ai"));
    }

    [Fact]
    public void The_owner_is_the_uuid_and_the_email()
    {
        ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json"))!.ToOwner()
            .ShouldBe(new TokenOwner("00000000-0000-0000-0000-000000000001", "max.muster@example.com"));
    }

    [Fact]
    public void The_email_is_optional()
    {
        ProfileParser.Parse("""{ "account": { "uuid": "abc" } }""")!.ToOwner().ShouldBe(new TokenOwner("abc", null));
    }

    [Fact]
    public void The_uuid_is_optional()
    {
        ProfileParser.Parse("""{ "account": { "email": "privat@example.com" } }""")!.ToOwner()
            .ShouldBe(new TokenOwner(null, "privat@example.com"));
    }

    [Fact]
    public void Without_an_organization_its_fields_stay_null()
    {
        var profile = ProfileParser.Parse("""{ "account": { "uuid": "abc", "has_claude_max": "yes" }, "organization": null }""");

        profile.ShouldBe(new AccountProfile("abc", null, null, null, null, null, null, null, null, null, null));
    }

    [Theory]
    [InlineData("""{ "account": { "uuid": " ", "email": "" } }""")]
    [InlineData("""{ "account": null }""")]
    [InlineData("""{ "organization": { "uuid": "abc" } }""")]
    [InlineData("[1, 2, 3]")]
    [InlineData("<html>rate limited</html>")]
    public void A_body_without_an_account_reads_as_null(string json)
    {
        ProfileParser.Parse(json).ShouldBeNull();
    }
}
