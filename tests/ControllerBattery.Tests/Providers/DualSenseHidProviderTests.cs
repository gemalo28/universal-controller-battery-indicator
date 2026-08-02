using ControllerBattery.Providers;

namespace ControllerBattery.Tests.Providers;

public sealed class DualSenseHidProviderTests
{
    [Theory]
    [InlineData(0x05, 55, false)]
    [InlineData(0x15, 55, true)]
    [InlineData(0x20, 100, false)]
    public void ParseBattery_ParsesUsbChargingStates(byte status, int expectedPercent, bool charging)
    {
        var report = new byte[64]; report[0] = 0x01; report[53] = status;
        var result = DualSenseHidProvider.ParseBattery(report);
        Assert.NotNull(result);
        Assert.Equal(expectedPercent, result.Percent);
        Assert.Equal(charging, result.IsCharging);
    }

    [Fact]
    public void ParseBattery_RejectsUnknownReport() =>
        Assert.Null(DualSenseHidProvider.ParseBattery([0x02, 0x00]));
}
