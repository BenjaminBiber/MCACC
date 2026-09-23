using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ClaudeCode;

public class CredentialsParserTests
{
    [Fact]
    public void Reads_the_access_token_and_the_expiry_in_milliseconds()
    {
        var credentials = CredentialsParser.Parse(FixtureFiles.ReadAllText("credentials-ok.json"));

        credentials.ShouldNotBeNull();
        credentials.AccessToken.ShouldBe("sk-ant-oat01-EXAMPLE-NOT-A-REAL-TOKEN");
        credentials.ExpiresAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1790000000000));
    }

    [Fact]
    public void A_missing_expiry_leaves_the_expiry_unknown()
    {
        var credentials = CredentialsParser.Parse("""{ "claudeAiOauth": { "accessToken": "t" } }""");

        credentials.ShouldBe(new OAuthCredentials("t", null));
    }

    [Theory]
    [InlineData("""{ "claudeAiOauth": { "expiresAt": 1790000000000 } }""")]
    [InlineData("""{ "claudeAiOauth": { "accessToken": "" } }""")]
    [InlineData("""{ "claudeAiOauth": { "accessToken": 42 } }""")]
    [InlineData("""{ "claudeAiOauth": null }""")]
    [InlineData("""{ "other": { "accessToken": "t" } }""")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("")]
    public void Anything_without_a_usable_token_parses_as_null(string json)
    {
        CredentialsParser.Parse(json).ShouldBeNull();
    }

    [Fact]
    public void A_non_numeric_expiry_leaves_the_expiry_unknown_but_keeps_the_token()
    {
        CredentialsParser.Parse("""{ "claudeAiOauth": { "accessToken": "t", "expiresAt": "soon" } }""")
            .ShouldBe(new OAuthCredentials("t", null));
    }

    [Fact]
    public void The_token_never_appears_in_the_text_of_the_record()
    {
        var credentials = new OAuthCredentials("sk-ant-oat01-SECRET", DateTimeOffset.UnixEpoch);

        credentials.ToString().ShouldNotContain("SECRET");
    }
}
