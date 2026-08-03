using ControllerBattery.Models;
using ControllerBattery.Providers;
using ControllerBattery.Services;
using ControllerBattery.Tests.Fakes;

namespace ControllerBattery.Tests.Integration;

public sealed class ControllerFamilyIntegrationTests
{
    public static TheoryData<string, string, string, int?, BatteryLevel, bool, bool, bool> Families =>
        new()
        {
            { "xinput", "Xbox-compatible", "Wireless", null, BatteryLevel.Low, false, true, false },
            { "sony-dualsense-hid", "PlayStation", "USB", 15, BatteryLevel.Low, false, true, true },
            { "sony-dualsense-hid", "PlayStation", "Bluetooth", 15, BatteryLevel.Low, true, true, true },
            { "nintendo-switch-pro-hid", "Nintendo", "USB", null, BatteryLevel.Low, false, true, false },
            { "nintendo-switch-pro-hid", "Nintendo", "Bluetooth", null, BatteryLevel.Low, true, true, false },
            { "8bitdo-hid", "8BitDo", "Bluetooth", 15, BatteryLevel.Low, false, false, false }
        };

    [Theory]
    [MemberData(nameof(Families))]
    public async Task Family_FlowsThroughMonitoringProfilesAndTransitions(
        string providerId, string kind, string connection, int? percent, BatteryLevel level,
        bool canPowerOff, bool canIdentify, bool canSetLed)
    {
        var original = Device(providerId, kind, connection, percent, level,
            canPowerOff, canIdentify, canSetLed);
        var provider = new FakeControllerProvider(providerId);
        provider.Enqueue(_ => Task.FromResult<IReadOnlyList<ControllerDevice>>([original]));
        var composite = new CompositeControllerProvider([provider]);
        await using var monitoring = new ControllerMonitoringService(composite);

        Assert.True(await monitoring.RefreshAsync(TestContext.Current.CancellationToken));
        var discovered = Assert.Single(monitoring.LatestSnapshot);
        Assert.Equal(providerId, discovered.ProviderId);
        Assert.Equal(kind, discovered.Kind);
        Assert.Equal(connection, discovered.Connection);

        var key = ControllerProfileService.DeviceKey(discovered);
        var profiles = new Dictionary<string, ControllerProfile>
        {
            [key] = new(key, $"My {kind}", "#123456", kind)
        };
        var projected = Assert.Single(ControllerProfileService.Apply([discovered], profiles));
        Assert.Equal($"My {kind}", projected.Name);
        Assert.Equal("#123456", projected.AccentColor);

        var connections = new ControllerConnectionService();
        Assert.Empty(connections.FindChanges([projected], profiles));
        var disconnected = Assert.Single(connections.FindChanges([], profiles));
        Assert.False(disconnected.IsConnected);
        Assert.Equal(projected.Name, disconnected.Controller.Name);

        var lowBattery = new LowBatteryService();
        Assert.Single(lowBattery.FindNewlyLow([projected]));
        Assert.Empty(lowBattery.FindNewlyLow([projected]));
    }

    [Theory]
    [MemberData(nameof(Families))]
    public async Task Family_ActionsRespectAdvertisedCapabilities(
        string providerId, string kind, string connection, int? percent, BatteryLevel level,
        bool canPowerOff, bool canIdentify, bool canSetLed)
    {
        var provider = new FakeControllerActionProvider(providerId);
        var service = new ControllerActionService(provider);
        var controller = Device(providerId, kind, connection, percent, level,
            canPowerOff, canIdentify, canSetLed);
        var token = TestContext.Current.CancellationToken;

        await AssertCapability(canPowerOff, () => service.PowerOffAsync(controller, token));
        await AssertCapability(canIdentify, () => service.IdentifyAsync(controller, token));
        await AssertCapability(canSetLed, () => service.SetLedAsync(controller, "#123456", 1, token));
        await AssertCapability(canSetLed, () => service.ResetLedAsync(controller, token));

        Assert.Equal(canPowerOff, provider.Calls.Contains("power"));
        Assert.Equal(canIdentify, provider.Calls.Contains("identify"));
        Assert.Equal(canSetLed, provider.Calls.Contains("led:#123456:1"));
        Assert.Equal(canSetLed, provider.Calls.Contains("reset"));
    }

    private static async Task AssertCapability(bool supported, Func<Task> action)
    {
        if (supported) await action();
        else await Assert.ThrowsAsync<NotSupportedException>(action);
    }

    private static ControllerDevice Device(string providerId, string kind, string connection,
        int? percent, BatteryLevel level, bool canPowerOff, bool canIdentify, bool canSetLed) =>
        new($"{providerId}-{connection}", providerId, $"{kind} Controller", kind, connection,
            percent, level, false, DateTime.UnixEpoch, CanPowerOff: canPowerOff,
            CanIdentify: canIdentify, CanSetLed: canSetLed);
}
