namespace Output.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset utcNow;
    private long timestamp;

    public ManualTimeProvider(DateTimeOffset? initialUtc = null)
    {
        utcNow = initialUtc ?? new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
        timestamp = utcNow.UtcTicks;
    }

    public override DateTimeOffset GetUtcNow() => utcNow;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override long GetTimestamp() => timestamp;

    public void Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delta, TimeSpan.Zero);
        utcNow += delta;
        timestamp += delta.Ticks;
    }
}
