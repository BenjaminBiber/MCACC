using Mcacc.Core.ClaudeCode;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ClaudeCode;

public class KeychainServiceNameTests
{
    [Fact]
    public void The_default_account_uses_the_plain_service_name()
    {
        KeychainServiceName.For(null).ShouldBe("Claude Code-credentials");
    }

    // The expected digests come from `printf '%s' <path> | sha256sum`.
    [Theory]
    [InlineData("/Users/USER/.mcacc/profiles/privat/claude", "Claude Code-credentials-98ddc4d3")]
    [InlineData(@"C:\Users\USER\.mcacc\profiles\privat\claude", "Claude Code-credentials-2bdd658d")]
    public void A_config_dir_gets_the_first_eight_hex_chars_of_its_sha256(string configDir, string expected)
    {
        KeychainServiceName.For(configDir).ShouldBe(expected);
    }

    [Fact]
    public void The_suffix_is_lower_case_hex_only()
    {
        var name = KeychainServiceName.For("/Users/Jö O'Neil’s 100%;$x/.mcacc/profiles/privat/claude");

        name.ShouldStartWith("Claude Code-credentials-");
        name[^8..].ShouldAllBe(c => char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f'));
    }
}
