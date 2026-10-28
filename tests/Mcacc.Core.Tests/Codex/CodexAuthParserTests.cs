using Mcacc.Core.Codex;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Codex;

public class CodexAuthParserTests
{
    private static CodexAuth Parse(string fixture) =>
        CodexAuthParser.Parse(FixtureFiles.ReadAllText(fixture)).ShouldNotBeNull();

    [Fact]
    public void Reads_the_identity_out_of_the_id_token_claims()
    {
        var login = Parse("codex-auth.json").Login.ShouldNotBeNull();

        login.Email.ShouldBe("ben@example.com");
        login.DisplayName.ShouldBe("Ben Biber");
        login.AccountUuid.ShouldBe("cff32eb3-0000-0000-0000-000000000000");
        login.PlanType.ShouldBe("plus");
    }

    /// <summary>Several organizations can be listed; the one marked as the default is the account's own.</summary>
    [Fact]
    public void Takes_the_organization_marked_as_the_default()
    {
        var login = Parse("codex-auth.json").Login.ShouldNotBeNull();

        login.OrganizationName.ShouldBe("Hauptorg");
        login.OrganizationRole.ShouldBe("admin");
    }

    [Fact]
    public void Reads_the_token_and_its_expiry_from_the_tokens_object()
    {
        var auth = Parse("codex-auth.json");

        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.AuthMode.ShouldBe("chatgpt");
        auth.HasApiKey.ShouldBeFalse();
        auth.AccessTokenExpiresAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1793000000));
    }

    /// <summary>The token must never reach a log or a test report through the record's own ToString.</summary>
    [Fact]
    public void ToString_hides_the_token()
    {
        var auth = Parse("codex-auth.json");

        auth.ToString().ShouldNotContain(auth.AccessToken!);
        auth.ToString().ShouldContain("<hidden>");
    }

    [Fact]
    public void An_api_key_login_has_no_token_and_no_claims()
    {
        var auth = Parse("codex-auth-apikey.json");

        auth.HasApiKey.ShouldBeTrue();
        auth.AccessToken.ShouldBeNull();
        auth.Login.ShouldBeNull();
        auth.AuthMode.ShouldBe("apikey");
        auth.ToString().ShouldContain("<none>");
    }

    /// <summary>An unreadable id token must not cost the token itself.</summary>
    [Fact]
    public void A_malformed_id_token_leaves_the_access_token_usable()
    {
        var auth = Parse("codex-auth-malformed-jwt.json");

        auth.Login.ShouldBeNull();
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_file_with_neither_a_token_nor_an_api_key_is_no_login_at_all()
    {
        CodexAuthParser.Parse(FixtureFiles.ReadAllText("codex-auth-empty.json")).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void Garbage_is_no_login_and_does_not_throw(string json)
    {
        CodexAuthParser.Parse(json).ShouldBeNull();
    }
}
