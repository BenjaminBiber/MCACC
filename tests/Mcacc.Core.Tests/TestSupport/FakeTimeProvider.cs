namespace Mcacc.Core.Tests.TestSupport;

internal sealed class FakeTimeProvider(DateTimeOffset utcNow, TimeZoneInfo localTimeZone) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;

    public override TimeZoneInfo LocalTimeZone => localTimeZone;
}
