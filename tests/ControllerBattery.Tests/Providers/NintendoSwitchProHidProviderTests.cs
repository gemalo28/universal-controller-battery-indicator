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

    [Theory]
    [InlineData(0x20, BatteryLevel.Low, false, null)]
    [InlineData(0x40, BatteryLevel.Medium, false, null)]
    [InlineData(0x60, BatteryLevel.High, false, null)]
    [InlineData(0x90, BatteryLevel.Full, true, "Fully charged")]
    [InlineData(0xE0, BatteryLevel.Unknown, false, null)]
    public void ParseBattery_CoversEveryNativeBand(byte status, BatteryLevel expected,
        bool charging, string? note)
    {
        var result = Assert.IsType<NintendoSwitchProHidProvider.BatteryObservation>(
            NintendoSwitchProHidProvider.ParseBattery([0x21, 0, status]));
        Assert.Equal(expected, result.Level);
        Assert.Equal(charging, result.IsCharging);
        if (note is not null) Assert.Contains(note, result.Note);
    }

    [Theory]
    [InlineData(true, 0x61, 0x58)]
    [InlineData(false, 0x01, 0x40)]
    public void CreateRumbleReport_WritesBothMotors(bool enabled, byte second, byte fourth)
    {
        var report = NintendoSwitchProHidProvider.CreateRumbleReport(16, 0x2F, enabled);
        Assert.Equal(0x10, report[0]);
        Assert.Equal(0x0F, report[1]);
        Assert.Equal(second, report[3]);
        Assert.Equal(fourth, report[5]);
        Assert.Equal(report[2..6], report[6..10]);
    }

    [Fact]
    public void CreateSubcommandReport_WritesNeutralMotorsAndCommand()
    {
        var report = NintendoSwitchProHidProvider.CreateSubcommandReport(16, 0x03, 0x30);
        Assert.Equal(0x01, report[0]);
        Assert.Equal([0x00, 0x01, 0x40, 0x40], report[2..6]);
        Assert.Equal([0x00, 0x01, 0x40, 0x40], report[6..10]);
        Assert.Equal(0x03, report[10]);
        Assert.Equal(0x30, report[11]);
    }
}
