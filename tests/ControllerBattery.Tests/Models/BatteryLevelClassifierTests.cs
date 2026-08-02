using ControllerBattery.Models;

namespace ControllerBattery.Tests.Models;

public sealed class BatteryLevelClassifierTests
{
    [Theory]
    [InlineData(null, BatteryLevel.Unknown)]
    [InlineData(5, BatteryLevel.Empty)]
    [InlineData(25, BatteryLevel.Low)]
    [InlineData(70, BatteryLevel.Medium)]
    [InlineData(99, BatteryLevel.High)]
    [InlineData(100, BatteryLevel.Full)]
    public void FromPercentage_ClassifiesBoundaries(int? value, BatteryLevel expected) =>
        Assert.Equal(expected, BatteryLevelClassifier.FromPercentage(value));
}
