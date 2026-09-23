using Mcacc.Core.Quoting;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Quoting;

public class PosixQuotingTests
{
    [Theory]
    [InlineData("", "''")]
    [InlineData("claude", "'claude'")]
    [InlineData("O'Brien", @"'O'\''Brien'")]
    [InlineData("''", @"''\'''\'''")]
    public void Wraps_in_single_quotes_and_closes_escapes_and_reopens_for_apostrophes(string value, string expected)
    {
        PosixQuoting.SingleQuote(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData("$HOME", "'$HOME'")]
    [InlineData("`id`", "'`id`'")]
    [InlineData(@"a\b", @"'a\b'")]
    [InlineData("say \"hi\"", "'say \"hi\"'")]
    [InlineData("Jö’s", "'Jö’s'")]
    [InlineData("a\nb", "'a\nb'")]
    public void Leaves_characters_that_are_literal_inside_single_quotes_alone(string value, string expected)
    {
        PosixQuoting.SingleQuote(value).ShouldBe(expected);
    }

    [Fact]
    public void Quotes_a_path_with_space_umlaut_both_apostrophes_percent_semicolon_and_dollar()
    {
        PosixQuoting.SingleQuote("/Users/Jö O'Neil’s 100%;$x")
            .ShouldBe(@"'/Users/Jö O'\''Neil’s 100%;$x'");
    }
}
