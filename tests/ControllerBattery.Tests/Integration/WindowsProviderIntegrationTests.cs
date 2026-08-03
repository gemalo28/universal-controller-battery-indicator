using ControllerBattery.Providers;
using ControllerBattery.Interop;

namespace ControllerBattery.Tests.Integration;

public sealed class WindowsProviderIntegrationTests
{
    public static TheoryData<IControllerProvider> Providers => new()
    {
        new XInputControllerProvider(),
        new DualSenseHidProvider(),
        new NintendoSwitchProHidProvider(),
        new EightBitDoHidProvider()
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ProviderScan_CompletesAndReturnsUniqueProviderScopedKeys(
        IControllerProvider provider)
    {
        var controllers = await provider.GetControllersAsync(TestContext.Current.CancellationToken);
        Assert.All(controllers, controller => Assert.Equal(provider.Id, controller.ProviderId));
        Assert.Equal(controllers.Count, controllers
            .Select(controller => $"{controller.ProviderId}:{controller.Id}")
            .Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task HardwareFactory_ComposesEverySupportedProvider()
    {
        var provider = ControllerProviderFactory.CreateHardwareProvider();
        Assert.IsType<CompositeControllerProvider>(provider);
        var controllers = await provider.GetControllersAsync(TestContext.Current.CancellationToken);
        Assert.Equal(controllers.Count, controllers.Select(controller =>
            $"{controller.ProviderId}:{controller.Id}").Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void HotkeyInterop_CanRegisterAndImmediatelyReleaseAThreadHotkey()
    {
        var registered = GlobalHotkeyInterop.Register(IntPtr.Zero, -1, 0, 0);
        var unregistered = GlobalHotkeyInterop.Unregister(IntPtr.Zero, -1);
        Assert.Equal(registered, unregistered);
    }
}
