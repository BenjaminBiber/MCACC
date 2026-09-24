namespace Mcacc.Core.Stats;

/// <summary>
/// The tokens of one UTC hour, one model, one project and one session. Hours rather than days let the query cut the
/// days in local time.
/// </summary>
/// <param name="Project">The working directory of the session; empty when the log does not say.</param>
public sealed record TokenBucket(
    DateTimeOffset Hour,
    string Model,
    string Project,
    string Session,
    TokenCounts Tokens,
    int Messages);
