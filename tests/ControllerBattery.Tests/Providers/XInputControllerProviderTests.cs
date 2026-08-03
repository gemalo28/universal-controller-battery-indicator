using ControllerBattery.Models;
using ControllerBattery.Providers;

namespace ControllerBattery.Tests.Providers;

public sealed class XInputControllerProviderTests
{
    [Theory]
    [InlineData(0, BatteryLevel.Empty)]
    [InlineData(1, BatteryLevel.Low)]
    [InlineData(2, BatteryLevel.Medium)]
    [InlineData(3, BatteryLevel.Full)]
    [InlineData(255, BatteryLevel.Unknown)]
    public void BatteryLevels_MapEveryXInputValue(byte value, BatteryLevel expected) =>
        Assert.Equal(expected, XInputControllerProvider.ToBatteryLevel(value));
}
