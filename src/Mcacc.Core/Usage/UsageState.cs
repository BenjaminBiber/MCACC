namespace Mcacc.Core.Usage;

public enum UsageState
{
    Ok,
    Expired,
    RateLimited,
    Unauthorized,
    NotLoggedIn,
    Error,
}
