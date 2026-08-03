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

    [Theory]
    [InlineData(0x0A, null, false, "voltage or temperature")]
    [InlineData(0x0B, null, false, "temperature error")]
    [InlineData(0x0F, null, false, "Charging error")]
    [InlineData(0x03, null, false, "Unknown charging state")]
    public void ParseBattery_DescribesNonstandardChargingStates(byte chargingState,
        int? expectedPercent, bool expectedCharging, string note)
    {
        var report = new byte[78];
        report[0] = 0x31;
        report[54] = (byte)((chargingState << 4) | 5);
        var result = Assert.IsType<DualSenseHidProvider.BatteryObservation>(
            DualSenseHidProvider.ParseBattery(report));
        Assert.Equal(expectedPercent, result.Percent);
        Assert.Equal(expectedCharging, result.IsCharging);
        Assert.Contains(note, result.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false, 0, 255, 128, 64, 255, 128, 64)]
    [InlineData(false, 1, 255, 128, 64, 153, 77, 38)]
    [InlineData(true, 2, 255, 128, 64, 77, 38, 19)]
    public void CreateLightbarReport_UsesTransportAndBrightness(bool bluetooth, byte brightness,
        byte red, byte green, byte blue, byte expectedRed, byte expectedGreen, byte expectedBlue)
    {
        var report = DualSenseHidProvider.CreateLightbarReport(
            bluetooth, red, green, blue, brightness);
        var offset = bluetooth ? 3 : 1;
        Assert.Equal(bluetooth ? 78 : 63, report.Length);
        Assert.Equal(bluetooth ? 0x31 : 0x02, report[0]);
        Assert.Equal(0x04, report[offset + 1]);
        Assert.Equal(expectedRed, report[offset + 44]);
        Assert.Equal(expectedGreen, report[offset + 45]);
        Assert.Equal(expectedBlue, report[offset + 46]);
        if (bluetooth) Assert.NotEqual([0, 0, 0, 0], report[74..78]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutputReports_HaveExpectedTransportEnvelope(bool bluetooth)
    {
        var release = DualSenseHidProvider.CreateReleaseLightbarReport(bluetooth);
        var rumble = DualSenseHidProvider.CreateRumbleReport(bluetooth, 0x1F, 10, 20);
        var offset = bluetooth ? 3 : 1;
        Assert.Equal(0x08, release[offset + 1]);
        Assert.Equal(bluetooth ? 78 : 63, rumble.Length);
        Assert.Equal(bluetooth ? 0x31 : 0x02, rumble[0]);
        Assert.Contains((byte)10, rumble);
        Assert.Contains((byte)20, rumble);
    }
}
