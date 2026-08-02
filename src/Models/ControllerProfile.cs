namespace ControllerBattery.Models;

public sealed record ControllerProfile(
    string DeviceKey,
    string? CustomName,
    string AccentColor,
    string? IconKind = null)
{
    public const string DefaultAccentColor = "#A99CF8";
}
