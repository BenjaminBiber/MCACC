using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public class TokenOwnerParserTests
{
    [Fact]
    public void Reads_the_account_uuid_and_email_but_not_the_organization()
    {
        var owner = TokenOwnerParser.Parse(FixtureFiles.ReadAllText("profile-ok.json"));

        owner.ShouldBe(new TokenOwner("00000000-0000-0000-0000-000000000001", "max.muster@example.com"));
    }

    [Fact]
    public void The_email_is_optional()
    {
        TokenOwnerParser.Parse("""{ "account": { "uuid": "abc" } }""").ShouldBe(new TokenOwner("abc", null));
    }

    [Fact]
    public void The_uuid_is_optional()
    {
        TokenOwnerParser.Parse("""{ "account": { "email": "privat@example.com" } }""")
            .ShouldBe(new TokenOwner(null, "privat@example.com"));
    }

    [Theory]
    [InlineData("""{ "account": { "uuid": " ", "email": "" } }""")]
    [InlineData("""{ "account": null }""")]
    [InlineData("""{ "organization": { "uuid": "abc" } }""")]
    [InlineData("[1, 2, 3]")]
    [InlineData("<html>rate limited</html>")]
    public void A_body_without_an_account_reads_as_null(string json)
    {
        TokenOwnerParser.Parse(json).ShouldBeNull();
    }
}
