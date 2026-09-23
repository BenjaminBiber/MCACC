using Mcacc.Core.Model;

namespace Mcacc.Core.Accounts;

public static class AccountNameValidator
{
    public const int MaxLength = 40;

    /// <returns>Null when the name is valid.</returns>
    public static NameValidationError? Validate(string name, IEnumerable<Account> existing, string? exceptId)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return NameValidationError.Empty;
        }

        if (trimmed.Length > MaxLength)
        {
            return NameValidationError.TooLong;
        }

        var isDuplicate = existing.Any(account =>
            account.Id != exceptId && string.Equals(account.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        return isDuplicate ? NameValidationError.Duplicate : null;
    }
}
