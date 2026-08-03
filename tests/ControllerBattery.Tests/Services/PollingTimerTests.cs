using ControllerBattery.Services;

namespace ControllerBattery.Tests.Services;

public sealed class PollingTimerTests
{
    [Fact]
    public async Task FactoryTimer_TicksAndDisposes()
    {
        IPollingTimerFactory factory = new PeriodicPollingTimerFactory();
        await using var timer = factory.Create(TimeSpan.FromMilliseconds(1));
        Assert.True(await timer.WaitForNextTickAsync(TestContext.Current.CancellationToken));
    }
}
