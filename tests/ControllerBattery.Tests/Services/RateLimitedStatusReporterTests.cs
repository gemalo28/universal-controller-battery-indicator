using ControllerBattery.Services;

namespace ControllerBattery.Tests;

public sealed class RateLimitedStatusReporterTests
{
    [Fact]
    public void TryReport_RateLimitsRepeatedReports()
    {
        var now = DateTimeOffset.UtcNow;
        var reporter = new RateLimitedStatusReporter(TimeSpan.FromSeconds(30), () => now);

        Assert.True(reporter.TryReport());
        Assert.False(reporter.TryReport());
        now += TimeSpan.FromSeconds(31);
        Assert.True(reporter.TryReport());
    }
}
