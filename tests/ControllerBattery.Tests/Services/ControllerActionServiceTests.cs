using ControllerBattery.Models;
using ControllerBattery.Services;
using ControllerBattery.Tests.Fakes;
using ControllerBattery.Providers;

namespace ControllerBattery.Tests.Services;

public sealed class ControllerActionServiceTests
{
    [Fact]
    public async Task RoutesSupportedCapabilities()
    {
        var other = new FakeControllerActionProvider("other");
        var matching = new FakeControllerActionProvider("actions");
        var service = new ControllerActionService(new CompositeControllerProvider([other, matching]));
        var controller = Device(canPowerOff: true, canIdentify: true, canSetLed: true);
        var token = TestContext.Current.CancellationToken;
        await service.PowerOffAsync(controller, token);
        await service.IdentifyAsync(controller, token);
        await service.SetLedAsync(controller, "#112233", 2, token);
        await service.ResetLedAsync(controller, token);
        Assert.Equal(["power", "identify", "led:#112233:2", "reset"], matching.Calls);
        Assert.Empty(other.Calls);
    }

    [Fact]
    public async Task UnsupportedCapabilitiesHaveDefinedBehavior()
    {
        var service = new ControllerActionService(new FakeControllerActionProvider());
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<NotSupportedException>(() => service.PowerOffAsync(Device(), token));
        await Assert.ThrowsAsync<NotSupportedException>(() => service.IdentifyAsync(Device(), token));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.SetLedAsync(Device(), "#FFFFFF", 0, token));
        await Assert.ThrowsAsync<NotSupportedException>(() => service.ResetLedAsync(Device(), token));
    }

    [Fact]
    public async Task CapabilityAndProviderInterfaceMismatch_FailsClearly()
    {
        var plain = new FakeControllerProvider("plain");
        var service = new ControllerActionService(new CompositeControllerProvider([plain]));
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.IdentifyAsync(Device(providerId: "plain", canIdentify: true),
                TestContext.Current.CancellationToken));
        Assert.Contains("plain", exception.Message);
        Assert.Contains("identification", exception.Message);
    }

    private static ControllerDevice Device(string providerId = "actions", bool canPowerOff = false,
        bool canIdentify = false, bool canSetLed = false) =>
        new("id", providerId, "Pad", "Test", "Wireless", 50, BatteryLevel.Medium, false,
            DateTime.UnixEpoch, CanPowerOff: canPowerOff, CanIdentify: canIdentify,
            CanSetLed: canSetLed);
}
