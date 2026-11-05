using Unit06.Common;

namespace Unit06.Exercise02;

public static class IdlePolicy
{
    public static TimeSpan Read(string? value)
    {
        if (value is null) return TimeSpan.FromSeconds(86400);
        if (!int.TryParse(value, out int seconds) || seconds is < 60 or > 86400)
            throw new ConfigurationException("MAF_MEMORY_IDLE_SECONDS must be an integer from 60 to 86400.");
        return TimeSpan.FromSeconds(seconds);
    }
}

// A controlled test clock. It is selected only by the explicit expiry verification.
public sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan interval) => _now += interval;
}
