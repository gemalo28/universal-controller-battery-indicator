using ControllerBattery.Interop;

namespace ControllerBattery.Tests.Interop;

public sealed class DeviceNotificationInteropTests
{
    [Theory]
    [InlineData(DeviceNotificationInterop.DeviceNodesChanged)]
    [InlineData(DeviceNotificationInterop.DeviceArrival)]
    [InlineData(DeviceNotificationInterop.DeviceRemoveComplete)]
    public void IsControllerDeviceChange_AcceptsRefreshEvents(int eventType) =>
        Assert.True(DeviceNotificationInterop.IsControllerDeviceChange(eventType));

    [Theory]
    [InlineData(0)]
    [InlineData(0x8001)]
    [InlineData(0x8003)]
    public void IsControllerDeviceChange_IgnoresUnrelatedEvents(int eventType) =>
        Assert.False(DeviceNotificationInterop.IsControllerDeviceChange(eventType));
}
