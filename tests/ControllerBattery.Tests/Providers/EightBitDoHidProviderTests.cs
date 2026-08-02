using ControllerBattery.Providers;

namespace ControllerBattery.Tests.Providers;

public sealed class EightBitDoHidProviderTests
{
    [Theory]
    [InlineData(0x01, 75, false)]
    [InlineData(0x04, 50, true)]
    public void ParseBattery_ParsesSupportedReports(byte reportId, byte percent, bool charging)
    {
        var report = new byte[15]; report[0] = reportId;
        report[14] = (byte)(percent | (charging ? 0x80 : 0));
        var result = EightBitDoHidProvider.ParseBattery(report);
        Assert.NotNull(result);
        Assert.Equal(percent, result.Percent);
        Assert.Equal(charging, result.IsCharging);
    }

    [Fact]
    public void ParseBattery_RejectsAmbiguousOrInvalidReports()
    {
        Assert.Null(EightBitDoHidProvider.ParseBattery(new byte[14]));
        var invalid = new byte[15]; invalid[0] = 0x01; invalid[14] = 127;
        Assert.Null(EightBitDoHidProvider.ParseBattery(invalid));
    }
}
