using Mcacc.Core.Accounts;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Accounts;

public class SlugGeneratorTests
{
    private static string Generate(string name) => SlugGenerator.Generate(name, _ => false);

    [Theory]
    [InlineData("Privat", "privat")]
    [InlineData("Kunde Müller & Söhne", "kunde-mueller-soehne")]
    [InlineData("Café", "cafe")]
    [InlineData("Crème Brûlée", "creme-brulee")]
    [InlineData("Señor", "senor")]
    [InlineData("ÄÖÜ Straße", "aeoeue-strasse")]
    [InlineData("Ærø Ångström", "aeroe-aangstroem")]
    [InlineData("  --Firma (alt)--  ", "firma-alt")]
    [InlineData("Team 42", "team-42")]
    [InlineData("com10", "com10")]
    public void Name_is_turned_into_a_lowercase_ascii_slug(string name, string expected)
    {
        Generate(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("&%!")]
    [InlineData("日本")]
    public void Name_without_usable_characters_falls_back_to_account(string name)
    {
        Generate(name).ShouldBe("account");
    }

    [Theory]
    [InlineData("Standard", "standard-2")]
    [InlineData("default", "default-2")]
    [InlineData("con", "con-2")]
    [InlineData("PRN", "prn-2")]
    [InlineData("aux", "aux-2")]
    [InlineData("nul", "nul-2")]
    [InlineData("com1", "com1-2")]
    [InlineData("COM9", "com9-2")]
    [InlineData("lpt1", "lpt1-2")]
    [InlineData("lpt9", "lpt9-2")]
    public void Reserved_name_gets_a_suffix(string name, string expected)
    {
        Generate(name).ShouldBe(expected);
    }

    [Fact]
    public void Taken_slug_gets_the_next_free_suffix()
    {
        SlugGenerator.Generate("Privat", slug => slug == "privat").ShouldBe("privat-2");
        SlugGenerator.Generate("Privat", slug => slug is "privat" or "privat-2").ShouldBe("privat-3");
    }

    [Fact]
    public void Taken_fallback_gets_a_suffix()
    {
        SlugGenerator.Generate("  ", slug => slug == "account").ShouldBe("account-2");
    }

    [Fact]
    public void Long_name_is_cut_to_32_chars_without_a_trailing_dash()
    {
        var name = new string('a', 31) + " " + new string('b', 18);
        name.Length.ShouldBe(50);

        Generate(name).ShouldBe(new string('a', 31));
    }

    [Fact]
    public void Long_name_without_a_dash_at_the_cut_keeps_32_chars()
    {
        Generate(new string('x', 50)).ShouldBe(new string('x', 32));
    }
}
