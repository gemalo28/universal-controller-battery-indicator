using ControllerBattery.Models;
using ControllerBattery.Providers;

namespace ControllerBattery.Tests.Providers;

public sealed class NintendoSwitchProHidProviderTests
{
    [Theory]
    [InlineData(0x00, BatteryLevel.Empty, false)]
    [InlineData(0x50, BatteryLevel.Medium, true)]
    [InlineData(0x80, BatteryLevel.Full, false)]
    public void ParseBattery_ParsesLevelAndCharging(byte status, BatteryLevel level, bool charging)
    {
        var result = NintendoSwitchProHidProvider.ParseBattery([0x30, 0, status]);
        Assert.NotNull(result);
        Assert.Equal(level, result.Level);
        Assert.Equal(charging, result.IsCharging);
    }

    [Fact]
    public void ParseBattery_RejectsUnrelatedReport() =>
        Assert.Null(NintendoSwitchProHidProvider.ParseBattery([0x01, 0, 0x80]));
}
