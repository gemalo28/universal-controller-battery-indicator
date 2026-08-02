namespace ControllerBattery.Models;

public sealed record ControllerProfile(
    string DeviceKey,
    string? CustomName,
    string AccentColor,
    string? IconKind = null,
    bool UseAccentForLed = false,
    string? LedColor = null,
    byte LedBrightness = 0,
    bool SyncLedWithProfile = false,
    string? ParentDeviceKey = null)
{
    public const string DefaultAccentColor = "#A99CF8";
}
