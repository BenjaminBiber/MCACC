using Mcacc.Core.Quoting;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Quoting;

public class PowerShellQuotingTests
{
    [Theory]
    [InlineData("", "''")]
    [InlineData("claude", "'claude'")]
    [InlineData("O'Brien", "'O''Brien'")]
    [InlineData("''", "''''''")]
    public void Wraps_in_single_quotes_and_doubles_ascii_apostrophes(string value, string expected)
    {
        PowerShellQuoting.SingleQuote(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData('\u2018')]
    [InlineData('\u2019')]
    [InlineData('\u201A')]
    [InlineData('\u201B')]
    public void Doubles_each_typographic_single_quote_with_itself(char quote)
    {
        PowerShellQuoting.SingleQuote($"a{quote}b").ShouldBe($"'a{quote}{quote}b'");
    }

    [Theory]
    [InlineData("$env:CLAUDE_CONFIG_DIR", "'$env:CLAUDE_CONFIG_DIR'")]
    [InlineData("a`b", "'a`b'")]
    [InlineData("say \"hi\"", "'say \"hi\"'")]
    [InlineData("100%;&|<>^", "'100%;&|<>^'")]
    public void Leaves_characters_that_are_literal_inside_single_quotes_alone(string value, string expected)
    {
        PowerShellQuoting.SingleQuote(value).ShouldBe(expected);
    }

    [Fact]
    public void Quotes_a_path_with_space_umlaut_both_apostrophes_percent_semicolon_and_dollar()
    {
        PowerShellQuoting.SingleQuote(@"C:\Users\Jö O'Neil’s 100%;$x")
            .ShouldBe(@"'C:\Users\Jö O''Neil’’s 100%;$x'");
    }
}
