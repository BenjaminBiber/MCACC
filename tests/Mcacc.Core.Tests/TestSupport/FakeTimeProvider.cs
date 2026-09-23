namespace Mcacc.Core.Tests.TestSupport;

/// <summary>
/// A clock that only moves when one of its timers fires. A timer fires right away on the thread pool and first moves
/// the clock to its due time, so <c>Task.Delay(…, time, ct)</c> takes no real time but its virtual time adds up exactly.
/// </summary>
internal sealed class FakeTimeProvider(DateTimeOffset utcNow, TimeZoneInfo localTimeZone) : TimeProvider
{
    private long _utcTicks = utcNow.UtcTicks;

    public override TimeZoneInfo LocalTimeZone => localTimeZone;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

    public override long GetTimestamp() => Interlocked.Read(ref _utcTicks);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new OneShotTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by) => Interlocked.Add(ref _utcTicks, by.Ticks);

    private sealed class OneShotTimer(FakeTimeProvider time, TimerCallback callback, object? state) : ITimer
    {
        private int _generation;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (period != Timeout.InfiniteTimeSpan)
            {
                throw new NotSupportedException("Only one-shot timers are supported.");
            }

            // A change or dispose bumps the generation, which cancels the callback queued before it.
            var generation = Interlocked.Increment(ref _generation);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                ThreadPool.UnsafeQueueUserWorkItem(_ =>
                {
                    if (Volatile.Read(ref _generation) == generation)
                    {
                        time.Advance(dueTime);
                        callback(state);
                    }
                }, null);
            }

            return true;
        }

        public void Dispose() => Interlocked.Increment(ref _generation);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
