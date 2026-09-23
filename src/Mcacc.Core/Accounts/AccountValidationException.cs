namespace Mcacc.Core.Accounts;

public sealed class AccountValidationException(NameValidationError error)
    : Exception($"The account name is invalid: {error}.")
{
    public NameValidationError Error { get; } = error;
}
