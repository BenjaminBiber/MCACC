using Mcacc.Core.Accounts;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Accounts;

public class AccountIdTests
{
    [Theory]
    [InlineData("standard")]
    [InlineData("privat")]
    [InlineData("kunde-mueller-soehne")]
    [InlineData("a")]
    [InlineData("com1-2")]
    [InlineData("42")]
    public void Slug_is_a_valid_id(string id)
    {
        AccountId.IsValid(id).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Privat")]
    [InlineData("-privat")]
    [InlineData("privat-")]
    [InlineData("privat--2")]
    [InlineData("privat_2")]
    [InlineData("privat.")]
    [InlineData("privat ")]
    [InlineData("...")]
    [InlineData(". .")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("müller")]
    [InlineData("privat\n")]
    public void Anything_else_is_not_a_valid_id(string? id)
    {
        AccountId.IsValid(id).ShouldBeFalse();
    }
}
