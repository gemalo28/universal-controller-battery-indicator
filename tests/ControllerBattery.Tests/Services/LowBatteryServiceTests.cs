using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery.Tests.Services;

public sealed class LowBatteryServiceTests
{
    [Fact]
    public void FindNewlyLow_OnlyReturnsTransitions()
    {
        var service = new LowBatteryService();
        var low = Device(BatteryLevel.Low);
        Assert.Single(service.FindNewlyLow([low]));
        Assert.Empty(service.FindNewlyLow([low]));
        Assert.Empty(service.FindNewlyLow([Device(BatteryLevel.Full)]));
        Assert.Single(service.FindNewlyLow([low]));
    }

    private static ControllerDevice Device(BatteryLevel level) => new("id", "provider", "Pad",
        "Test", "Wireless", null, level, false, DateTime.UnixEpoch);
}
