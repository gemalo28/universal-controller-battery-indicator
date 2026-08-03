using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery.Tests.Services;

public sealed class ControllerConnectionServiceTests
{
    [Fact]
    public void FindChanges_SuppressesInitialSnapshotAndReportsTransitions()
    {
        var service = new ControllerConnectionService();
        var first = Device("one", "First");
        var second = Device("two", "Second");

        Assert.Empty(service.FindChanges([first], NoProfiles));
        var connected = Assert.Single(service.FindChanges([first, second], NoProfiles));
        Assert.True(connected.IsConnected);
        Assert.Equal(second, connected.Controller);

        var disconnected = Assert.Single(service.FindChanges([second], NoProfiles));
        Assert.False(disconnected.IsConnected);
        Assert.Equal(first, disconnected.Controller);
    }

    [Fact]
    public void FindChanges_UsesAndDeduplicatesGroupMainController()
    {
        var service = new ControllerConnectionService();
        var main = Device("main", "Living Room Controller", "hid");
        var output = Device("output", "Game Controller", "xinput");
        var mainKey = ControllerProfileService.DeviceKey(main);
        var profiles = new Dictionary<string, ControllerProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [ControllerProfileService.DeviceKey(output)] = new(
                ControllerProfileService.DeviceKey(output), null,
                ControllerProfile.DefaultAccentColor, ParentDeviceKey: mainKey)
        };

        Assert.Empty(service.FindChanges([], profiles));
        var connected = Assert.Single(service.FindChanges([main, output], profiles));
        Assert.True(connected.IsConnected);
        Assert.Equal(main, connected.Controller);

        var disconnected = Assert.Single(service.FindChanges([], profiles));
        Assert.False(disconnected.IsConnected);
        Assert.Equal(main, disconnected.Controller);
    }

    private static readonly IReadOnlyDictionary<string, ControllerProfile> NoProfiles =
        new Dictionary<string, ControllerProfile>();

    private static ControllerDevice Device(string id, string name, string provider = "test") =>
        new(id, provider, name, "Test", "Wireless", 80, BatteryLevel.Full, false,
            DateTime.UnixEpoch);
}
