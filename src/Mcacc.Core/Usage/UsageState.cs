namespace Mcacc.Core.Usage;

public enum UsageState
{
    Ok,
    Expired,
    RateLimited,
    Unauthorized,
    NotLoggedIn,
    Error,

    /// <summary>No fresh value could be had; the snapshot is a local one and its age is shown.</summary>
    Stale,

    /// <summary>The Claude Code token belongs to another account than the one <c>.claude.json</c> names.</summary>
    WrongAccount,
}
