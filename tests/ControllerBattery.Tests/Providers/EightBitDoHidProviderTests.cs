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

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x03)]
    [InlineData(0x04)]
    public void ParseBattery_AllEnhancedReportIdsSupportFullCharge(byte reportId)
    {
        var report = new byte[15]; report[0] = reportId; report[14] = 100;
        var result = Assert.IsType<EightBitDoHidProvider.BatteryObservation>(
            EightBitDoHidProvider.ParseBattery(report));
        Assert.Equal(100, result.Percent);
        Assert.Equal("Fully charged", result.Note);
    }

    [Theory]
    [InlineData("hid#vid_2dc8&bth", "Bluetooth")]
    [InlineData("hid#vid_2dc8&pid_3106", "USB / 2.4 GHz")]
    public void ConnectionAndUnavailableNote_AreTransportSpecific(string path, string connection)
    {
        Assert.Equal(connection, EightBitDoHidProvider.GetConnection(path));
        Assert.Contains("not exposed", EightBitDoHidProvider.GetBatteryNote(connection));
    }

    [Theory]
    [InlineData(0x05, true)]
    [InlineData(0x04, true)]
    [InlineData(0x06, false)]
    public void ContainsUsage_RecognizesGameControllerCollections(byte usage, bool expected)
    {
        byte[] descriptor = [0x00, 0x05, 0x01, 0x09, usage, 0x00];
        Assert.Equal(expected, EightBitDoHidProvider.ContainsUsage(descriptor, usage: 0x05) ||
            EightBitDoHidProvider.ContainsUsage(descriptor, usage: 0x04));
    }
}
