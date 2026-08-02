using ControllerBattery.Models;
using ControllerBattery.Services;
using ControllerBattery.Tests.Fakes;

namespace ControllerBattery.Tests.Services;

public sealed class ControllerActionServiceTests
{
    [Fact]
    public async Task RoutesSupportedCapabilities()
    {
        var provider = new FakeControllerActionProvider();
        var service = new ControllerActionService(provider);
        var controller = Device();
        var token = TestContext.Current.CancellationToken;
        await service.PowerOffAsync(controller, token);
        await service.IdentifyAsync(controller, token);
        await service.SetLedAsync(controller, "#112233", 2, token);
        await service.ResetLedAsync(controller, token);
        Assert.Equal(["power", "identify", "led:#112233:2", "reset"], provider.Calls);
    }

    [Fact]
    public async Task UnsupportedCapabilitiesHaveDefinedBehavior()
    {
        var provider = new FakeControllerProvider();
        var service = new ControllerActionService(provider);
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<NotSupportedException>(() => service.PowerOffAsync(Device(), token));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.SetLedAsync(Device(), "#FFFFFF", 0, token));
        await service.IdentifyAsync(Device(), token);
    }

    private static ControllerDevice Device() => new("id", "actions", "Pad", "Test", "Wireless",
        50, BatteryLevel.Medium, false, DateTime.UnixEpoch);
}
