using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery.Tests.Models;

public sealed class ControllerProfileTests
{
    [Fact]
    public void Normalize_RepairsColorsAndSelfParent()
    {
        var profile = new ControllerProfile("device", "  Name  ", "invalid", "Unknown",
            UseAccentForLed: true, ParentDeviceKey: "device", LedBrightness: 9);
        var normalized = ControllerProfileService.Normalize(profile);
        Assert.Equal("Name", normalized.CustomName);
        Assert.Equal(ControllerProfile.DefaultAccentColor, normalized.AccentColor);
        Assert.Equal(ControllerProfile.DefaultAccentColor, normalized.LedColor);
        Assert.Null(normalized.IconKind);
        Assert.Null(normalized.ParentDeviceKey);
        Assert.Equal(0, normalized.LedBrightness);
    }

    [Fact]
    public void Normalize_UppercasesValidColors()
    {
        var normalized = ControllerProfileService.Normalize(
            new("device", null, "#aabbcc", LedColor: "#010aef"));
        Assert.Equal("#AABBCC", normalized.AccentColor);
        Assert.Equal("#010AEF", normalized.LedColor);
    }
}
