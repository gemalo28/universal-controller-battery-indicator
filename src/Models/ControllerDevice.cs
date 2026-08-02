namespace ControllerBattery.Models;

public sealed record ControllerDevice(
    string Id,
    string ProviderId,
    string Name,
    string Kind,
    string Connection,
    int? BatteryPercent,
    BatteryLevel BatteryLevel,
    bool IsCharging,
    DateTime UpdatedAt,
    string? BatteryNote = null,
    string? HardwareId = null,
    bool CanPowerOff = false,
    bool CanIdentify = false,
    string? AccentColor = null,
    string? ProfileIconKind = null,
    bool CanSetLed = false);
