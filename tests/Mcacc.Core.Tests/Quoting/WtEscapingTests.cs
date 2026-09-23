using Mcacc.Core.Quoting;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Quoting;

public class WtEscapingTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("claude", "claude")]
    [InlineData("a; b", @"a\; b")]
    [InlineData(";;", @"\;\;")]
    [InlineData(@"C:\Users\Jö O'Neil’s 100%;$x", @"C:\Users\Jö O'Neil’s 100%\;$x")]
    [InlineData("$env:A='x'; claude", @"$env:A='x'\; claude")]
    public void Escapes_every_semicolon_with_a_backslash(string value, string expected)
    {
        WtEscaping.EscapeSemicolons(value).ShouldBe(expected);
    }
}
