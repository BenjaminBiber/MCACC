namespace Mcacc.Core.Usage;

public enum UsageState
{
    Ok,
    Expired,
    RateLimited,
    Unauthorized,
    NotLoggedIn,
    Error,

    /// <summary>The Claude Code token belongs to another account than the one <c>.claude.json</c> names.</summary>
    WrongAccount,
}
