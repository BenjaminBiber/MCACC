using System.Text.RegularExpressions;

namespace Mcacc.Core.Accounts;

public static partial class AccountId
{
    /// <summary>A slug such as <c>kunde-mueller</c>; profile paths are only ever derived from valid ids.</summary>
    public static bool IsValid(string? id) => id is not null && Pattern().IsMatch(id);

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex Pattern();
}
