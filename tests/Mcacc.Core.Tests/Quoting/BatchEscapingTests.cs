using Mcacc.Core.Quoting;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Quoting;

public class BatchEscapingTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("100%", "100%%")]
    [InlineData("%USERPROFILE%", "%%USERPROFILE%%")]
    [InlineData("%%", "%%%%")]
    public void Doubles_percent_signs(string value, string expected)
    {
        BatchEscaping.ForSetValue(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData("a&b|c<d>e^f(g)!h")]
    [InlineData("a;b,c=d")]
    [InlineData("O'Neil’s $x")]
    public void Leaves_characters_that_are_literal_inside_a_quoted_set_alone(string value)
    {
        BatchEscaping.ForSetValue(value).ShouldBe(value);
    }

    [Fact]
    public void Escapes_a_path_with_space_umlaut_both_apostrophes_percent_semicolon_and_dollar()
    {
        BatchEscaping.ForSetValue(@"C:\Users\Jö O'Neil’s 100%;$x")
            .ShouldBe(@"C:\Users\Jö O'Neil’s 100%%;$x");
    }

    [Theory]
    [InlineData("C:\\a\"b")]
    [InlineData("C:\\a\rb")]
    [InlineData("C:\\a\nb")]
    [InlineData("\r\n")]
    public void Refuses_quotes_and_line_breaks(string value)
    {
        Should.Throw<ArgumentException>(() => BatchEscaping.ForSetValue(value));
    }
}
