using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery.Tests.Services;

public sealed class ControllerProfileServiceTests
{
    [Fact]
    public void Apply_UsesProviderScopedProfile()
    {
        var device = new ControllerDevice("id", "provider", "Original", "Test", "Wireless",
            50, BatteryLevel.Medium, false, DateTime.UnixEpoch);
        var profiles = new Dictionary<string, ControllerProfile>
            { ["provider:id"] = new("provider:id", "Custom", "#112233", "Generic") };
        var result = Assert.Single(ControllerProfileService.Apply([device], profiles));
        Assert.Equal("Custom", result.Name);
        Assert.Equal("#112233", result.AccentColor);
    }
}
