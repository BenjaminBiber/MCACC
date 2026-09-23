using Mcacc.Core.Accounts;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Accounts;

public class AccountNameValidatorTests
{
    private static readonly Account[] Existing =
    [
        Account.CreateDefault(),
        new("privat", "Privat", "#6A9BCC", IsDefault: false),
        new("mueller", "  Kunde Müller ", "#7D9B76", IsDefault: false),
    ];

    [Theory]
    [InlineData("Firma")]
    [InlineData("  Firma  ")]
    [InlineData("Privat 2")]
    public void Unique_name_is_valid(string name)
    {
        AccountNameValidator.Validate(name, Existing, exceptId: null).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Blank_name_is_empty(string name)
    {
        AccountNameValidator.Validate(name, Existing, exceptId: null).ShouldBe(NameValidationError.Empty);
    }

    [Fact]
    public void Name_of_exactly_40_chars_is_valid_even_with_surrounding_whitespace()
    {
        AccountNameValidator.Validate("  " + new string('a', 40) + "  ", Existing, exceptId: null).ShouldBeNull();
    }

    [Fact]
    public void Name_over_40_chars_is_too_long()
    {
        AccountNameValidator.Validate(new string('a', 41), Existing, exceptId: null)
            .ShouldBe(NameValidationError.TooLong);
    }

    [Theory]
    [InlineData("Privat")]
    [InlineData("privat")]
    [InlineData("  PRIVAT ")]
    [InlineData("standard")]
    [InlineData("kunde müller")]
    [InlineData("KUNDE MÜLLER")]
    public void Name_matching_another_account_case_insensitively_is_a_duplicate(string name)
    {
        AccountNameValidator.Validate(name, Existing, exceptId: null).ShouldBe(NameValidationError.Duplicate);
    }

    [Fact]
    public void Own_name_is_not_a_duplicate_when_excluded()
    {
        AccountNameValidator.Validate("PRIVAT", Existing, exceptId: "privat").ShouldBeNull();
    }

    [Fact]
    public void Excluding_self_still_detects_other_duplicates()
    {
        AccountNameValidator.Validate("Standard", Existing, exceptId: "privat").ShouldBe(NameValidationError.Duplicate);
    }
}
