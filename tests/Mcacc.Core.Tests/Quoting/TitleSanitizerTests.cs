using Mcacc.Core.Quoting;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Quoting;

public class TitleSanitizerTests
{
    [Theory]
    [InlineData(";")]
    [InlineData("&")]
    [InlineData("|")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("^")]
    [InlineData("%")]
    [InlineData("\"")]
    [InlineData("'")]
    public void Removes_characters_that_are_special_in_a_shell_or_wt(string special)
    {
        TitleSanitizer.Sanitize($"a{special}b").ShouldBe("ab");
    }

    [Theory]
    [InlineData("a\tb")]
    [InlineData("a\r\nb")]
    [InlineData("a\u0000b")]
    [InlineData("a\u0007b")]
    [InlineData("a\u001Bb")]
    [InlineData("a\u007Fb")]
    [InlineData("a\u0085b")]
    public void Removes_control_characters(string value)
    {
        TitleSanitizer.Sanitize(value).ShouldBe("ab");
    }

    [Theory]
    [InlineData("a   b", "a b")]
    [InlineData("a ; b", "a b")]
    [InlineData("  a b  ", "a b")]
    [InlineData(" ; ", "")]
    [InlineData("", "")]
    public void Collapses_and_trims_spaces(string value, string expected)
    {
        TitleSanitizer.Sanitize(value).ShouldBe(expected);
    }

    [Fact]
    public void Collapses_other_whitespace_such_as_no_break_and_line_separators_into_one_space()
    {
        const char noBreakSpace = (char)0x00A0;
        const char lineSeparator = (char)0x2028;

        TitleSanitizer.Sanitize($"a {noBreakSpace}{lineSeparator} b").ShouldBe("a b");
    }

    [Fact]
    public void Keeps_umlauts_middle_dot_typographic_apostrophe_dollar_and_parentheses()
    {
        TitleSanitizer.Sanitize("Claude Code · Jö’s $x (1)").ShouldBe("Claude Code · Jö’s $x (1)");
    }

    [Fact]
    public void Cuts_to_60_characters()
    {
        TitleSanitizer.Sanitize(new string('x', 61)).ShouldBe(new string('x', 60));
        TitleSanitizer.Sanitize(new string('x', 60)).ShouldBe(new string('x', 60));
    }

    [Fact]
    public void Trims_a_space_left_at_the_cut()
    {
        TitleSanitizer.Sanitize(new string('x', 59) + " yz").ShouldBe(new string('x', 59));
    }

    [Fact]
    public void Does_not_split_a_surrogate_pair_at_the_cut()
    {
        TitleSanitizer.Sanitize(new string('x', 59) + "😀z").ShouldBe(new string('x', 59));
        TitleSanitizer.Sanitize(new string('x', 58) + "😀z").ShouldBe(new string('x', 58) + "😀");
    }
}
