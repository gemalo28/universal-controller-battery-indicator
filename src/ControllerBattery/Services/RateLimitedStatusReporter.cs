namespace ControllerBattery.Services;

public sealed class RateLimitedStatusReporter(TimeSpan interval, Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;

    public bool TryReport()
    {
        var now = _clock();
        if (now < _nextAllowed) return false;
        _nextAllowed = now + interval;
        return true;
    }
}
