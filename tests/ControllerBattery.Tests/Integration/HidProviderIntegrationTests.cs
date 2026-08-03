using ControllerBattery.Models;
using ControllerBattery.Providers;
using ControllerBattery.Tests.Fakes;

namespace ControllerBattery.Tests.Integration;

public sealed class HidProviderIntegrationTests
{
    [Theory]
    [InlineData(null, "Nintendo Switch Pro Controller")]
    [InlineData("Wireless Gamepad", "Nintendo Switch Pro Controller")]
    [InlineData("Gamepad", "Nintendo Switch Pro Controller")]
    [InlineData("Pro Controller", "Nintendo Switch Pro Controller")]
    [InlineData("Wireless Controller", "Nintendo Switch Pro Controller")]
    [InlineData("  Custom Pro  ", "Custom Pro")]
    public void SwitchPro_ProductNamesAreNormalized(string? product, string expected) =>
        Assert.Equal(expected, NintendoSwitchProHidProvider.NormalizeProductName(product));

    [Theory]
    [InlineData("SERIAL", "057E:2009:SERIAL")]
    [InlineData(null, "057E:2009:USB#path")]
    public void SwitchPro_HardwareIdentityPrefersSerial(string? serial, string expected) =>
        Assert.Equal(expected, NintendoSwitchProHidProvider.BuildHardwareId(
            0x057E, 0x2009, serial, "USB#path"));

    [Theory]
    [InlineData(false, 0x05, 55, false)]
    [InlineData(true, 0x15, 55, true)]
    public async Task DualSense_ScanAndOutputsUseInjectedHidDevice(bool bluetooth, byte status,
        int percent, bool charging)
    {
        var streams = new List<FakeHidStream>();
        FakeHidDevice? device = null;
        device = new(0x054C, 0x0CE6, bluetooth ? "BTH#dualsense" : "USB#dualsense",
            bluetooth ? 78 : 64, bluetooth ? 78 : 63, () =>
            {
                var report = new byte[bluetooth ? 78 : 64];
                report[0] = bluetooth ? (byte)0x31 : (byte)0x01;
                report[bluetooth ? 54 : 53] = status;
                var stream = new FakeHidStream(device!, [report]);
                streams.Add(stream);
                return stream;
            });
        var disconnectCalled = false;
        var provider = new DualSenseHidProvider(() => [device], _ => disconnectCalled = true);

        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(percent, controller.BatteryPercent);
        Assert.Equal(charging, controller.IsCharging);
        Assert.Equal(bluetooth, controller.CanPowerOff);
        Assert.True(controller.CanIdentify);
        Assert.True(controller.CanSetLed);

        await provider.SetLedColorAsync(controller, "#123456", 1,
            TestContext.Current.CancellationToken);
        await provider.ResetLedAsync(controller, TestContext.Current.CancellationToken);
        await provider.PulseAsync(controller, TestContext.Current.CancellationToken);
        Assert.Contains(streams.SelectMany(stream => stream.Writes), write =>
            write[0] is 0x02 or 0x31);

        if (bluetooth)
        {
            await provider.PowerOffAsync(controller, TestContext.Current.CancellationToken);
            Assert.True(disconnectCalled);
        }
        else
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => provider.PowerOffAsync(controller,
                TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData(typeof(TimeoutException), "waiting")]
    [InlineData(typeof(UnauthorizedAccessException), "denied")]
    [InlineData(typeof(IOException), "No battery")]
    public async Task DualSense_ScanHandlesUnavailableReports(Type exceptionType,
        string expectedNote)
    {
        FakeHidDevice? device = null;
        device = new(0x054C, 0x0CE6, "USB#dualsense-error", 64, 63, () =>
            new FakeHidStream(device!)
            {
                ReadException = (Exception)Activator.CreateInstance(exceptionType)!
            });
        var provider = new DualSenseHidProvider(() => [device], _ => { });

        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));

        Assert.Contains(expectedNote, controller.BatteryNote,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DualSense_UnavailableDeviceRejectsEveryOutputAction()
    {
        FakeHidDevice? device = null;
        device = new(0x054C, 0x0CE6, "USB#dualsense-busy", 64, 63,
            () => throw new IOException("busy"));
        var provider = new DualSenseHidProvider(() => [device], _ => { });
        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));
        Assert.Contains("use", controller.BatteryNote, StringComparison.OrdinalIgnoreCase);

        await Assert.ThrowsAsync<IOException>(() => provider.PulseAsync(controller,
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<IOException>(() => provider.SetLedColorAsync(controller,
            "#112233", 1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<IOException>(() => provider.ResetLedAsync(controller,
            TestContext.Current.CancellationToken));

        var absent = new DualSenseHidProvider(() => [], _ => { });
        await Assert.ThrowsAsync<IOException>(() => absent.PulseAsync(controller,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, 0x80, BatteryLevel.Full, false)]
    [InlineData(true, 0x50, BatteryLevel.Medium, true)]
    public async Task SwitchPro_ScanAndActionsUseInjectedHidDevice(bool bluetooth, byte status,
        BatteryLevel level, bool charging)
    {
        var streams = new List<FakeHidStream>();
        FakeHidDevice? device = null;
        device = new(0x057E, 0x2009, bluetooth ? "BTH#pro" : "USB#pro", 64, 64, () =>
        {
            var report = new byte[64]; report[0] = 0x30; report[2] = status;
            var stream = new FakeHidStream(device!, [report]);
            streams.Add(stream);
            return stream;
        });
        var disconnectCalled = false;
        var provider = new NintendoSwitchProHidProvider(() => [device],
            _ => disconnectCalled = true);

        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(level, controller.BatteryLevel);
        Assert.Equal(charging, controller.IsCharging);
        Assert.Equal(bluetooth, controller.CanPowerOff);
        Assert.True(controller.CanIdentify);
        await provider.PulseAsync(controller, TestContext.Current.CancellationToken);

        if (bluetooth)
        {
            await provider.PowerOffAsync(controller, TestContext.Current.CancellationToken);
            Assert.True(disconnectCalled);
        }
        else
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => provider.PowerOffAsync(controller,
                TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task HidProviders_PropagateCancellationBeforeScanningDevices()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var dualSense = new DualSenseHidProvider(() => throw new Xunit.Sdk.XunitException(
            "Enumeration should not occur"), _ => { });
        var switchPro = new NintendoSwitchProHidProvider(() => throw new Xunit.Sdk.XunitException(
            "Enumeration should not occur"), _ => { });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dualSense.GetControllersAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            switchPro.GetControllersAsync(cancellation.Token));
    }

    [Theory]
    [InlineData("BTH#8bitdo", 0x81, 1, true)]
    [InlineData("USB#8bitdo", 75, 75, false)]
    public async Task EightBitDo_ScanUsesInjectedGameController(string path, byte power,
        int percent, bool charging)
    {
        FakeHidDevice? device = null;
        device = new(0x2DC8, 0x3106, path, 34, 34, () =>
        {
            var report = new byte[15]; report[0] = 0x01; report[14] = power;
            return new FakeHidStream(device!, [report]);
        });
        var provider = new EightBitDoHidProvider(() => [device], _ => true);
        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(percent, controller.BatteryPercent);
        Assert.Equal(charging, controller.IsCharging);
        Assert.Equal(path.StartsWith("BTH") ? "Bluetooth" : "USB / 2.4 GHz",
            controller.Connection);
    }

    [Fact]
    public async Task EightBitDo_SkipsNonControllersAndAmbiguousXInputCompanions()
    {
        FakeHidDevice? ignored = null;
        ignored = new(0x2DC8, 1, "USB#ignored", 34, 34,
            () => new FakeHidStream(ignored!));
        FakeHidDevice? companion = null;
        companion = new(0x2DC8, 2, "USB&IG_companion", 34, 34,
            () => new FakeHidStream(companion!));
        var provider = new EightBitDoHidProvider(() => [ignored, companion],
            device => ReferenceEquals(device, companion));
        Assert.Empty(await provider.GetControllersAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null, "Nintendo Switch Pro Controller")]
    [InlineData("Wireless Gamepad", "Nintendo Switch Pro Controller")]
    [InlineData("Gamepad", "Nintendo Switch Pro Controller")]
    [InlineData("Pro Controller", "Nintendo Switch Pro Controller")]
    [InlineData("Wireless Controller", "Nintendo Switch Pro Controller")]
    [InlineData("  Custom Pro  ", "Custom Pro")]
    public async Task SwitchPro_NormalizesProductNamesAndUsesSerialIdentity(string? product,
        string expected)
    {
        FakeHidDevice? device = null;
        device = new(0x057E, 0x2009, "USB#name", 64, 64,
            () => new FakeHidStream(device!), product, "SERIAL");
        var provider = new NintendoSwitchProHidProvider(() => [device], _ => { });

        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(product is "  Custom Pro  " ? "Nintendo Switch Pro Controller" : expected,
            controller.Name);
        Assert.Contains("USB#name", controller.Id);
        Assert.Equal(BatteryLevel.Unknown, controller.BatteryLevel);
    }

    [Fact]
    public async Task SwitchPro_HandlesIoFailuresAndUnavailableActions()
    {
        FakeHidDevice? failing = null;
        failing = new(0x057E, 0x2009, "BTH#failure", 64, 64,
            () => new FakeHidStream(failing!) { ReadException = new IOException("read") })
        { ProductException = new IOException("name"), SerialException = new IOException("serial") };
        var provider = new NintendoSwitchProHidProvider(() => [failing], _ => { });
        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));
        Assert.Contains("No Nintendo", controller.BatteryNote);
        Assert.Equal("Nintendo Switch Pro Controller", controller.Name);

        var absent = new NintendoSwitchProHidProvider(() => [], _ => { });
        await Assert.ThrowsAsync<IOException>(() => absent.PulseAsync(controller,
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<IOException>(() => absent.PowerOffAsync(controller,
            TestContext.Current.CancellationToken));

        FakeHidDevice? tooSmall = null;
        tooSmall = new(0x057E, 0x2009, "USB#small", 64, 8,
            () => new FakeHidStream(tooSmall!, [[0x30, 0, 0x80]]));
        var smallProvider = new NintendoSwitchProHidProvider(() => [tooSmall], _ => { });
        var smallController = Assert.Single(await smallProvider.GetControllersAsync(
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<IOException>(() => smallProvider.PulseAsync(smallController,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(TimeoutException))]
    public async Task SwitchPro_InitializationFailuresDoNotHideController(Type exceptionType)
    {
        FakeHidDevice? device = null;
        device = new(0x057E, 0x2009, "USB#init", 64, 64, () =>
            new FakeHidStream(device!, [[0x30, 0, 0x80]])
            {
                WriteException = (Exception)Activator.CreateInstance(exceptionType)!
            });
        var provider = new NintendoSwitchProHidProvider(() => [device], _ => { });
        Assert.Single(await provider.GetControllersAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task EightBitDo_ReadFailuresReturnAnUnknownBattery(Type exceptionType)
    {
        FakeHidDevice? device = null;
        device = new(0x2DC8, 0x3106, "USB#failure", 34, 34, () =>
            new FakeHidStream(device!)
            {
                ReadException = (Exception)Activator.CreateInstance(exceptionType)!
            }, "  Pro  ", "SERIAL");
        var provider = new EightBitDoHidProvider(() => [device], _ => true);

        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));

        Assert.Null(controller.BatteryPercent);
        Assert.Equal("8BitDo Controller", controller.Name);
        Assert.Contains("USB#failure", controller.Id);
    }

    [Fact]
    public async Task EightBitDo_DescriptorAndMetadataFailuresAreHandled()
    {
        FakeHidDevice? invalidDescriptor = null;
        invalidDescriptor = new(0x2DC8, 1, "USB#descriptor", 34, 34,
            () => new FakeHidStream(invalidDescriptor!))
        { DescriptorException = new IOException("descriptor") };
        var classifier = new EightBitDoHidProvider(() => [invalidDescriptor]);
        Assert.Empty(await classifier.GetControllersAsync(TestContext.Current.CancellationToken));

        FakeHidDevice? metadata = null;
        metadata = new(0x2DC8, 2, "USB#metadata", 34, 34,
            () => new FakeHidStream(metadata!), descriptor: [0x05, 0x01, 0x09, 0x05])
        { ProductException = new IOException("product"), SerialException = new IOException("serial") };
        var provider = new EightBitDoHidProvider(() => [metadata], _ => true);
        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("8BitDo Controller", controller.Name);
        Assert.Contains("USB#metadata", controller.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EightBitDo_DefaultClassifierHandlesBusyAndIdleControllers(bool busy)
    {
        FakeHidDevice? device = null;
        device = new(0x2DC8, 0x3106, busy ? "USB#busy" : "USB#idle", 34, 34,
            () => busy ? throw new IOException("busy") : new FakeHidStream(device!));
        var provider = new EightBitDoHidProvider(() => [device]);

        var controller = Assert.Single(await provider.GetControllersAsync(
            TestContext.Current.CancellationToken));

        Assert.Null(controller.BatteryPercent);
    }

    [Fact]
    public async Task SwitchPro_BusyAndDeniedDevicesRemainVisibleAndRejectPulse()
    {
        FakeHidDevice? busy = null;
        busy = new(0x057E, 0x2009, "USB#switch-busy", 64, 64,
            () => throw new IOException("busy"));
        var busyProvider = new NintendoSwitchProHidProvider(() => [busy], _ => { });
        var busyController = Assert.Single(await busyProvider.GetControllersAsync(
            TestContext.Current.CancellationToken));
        Assert.Contains("use", busyController.BatteryNote, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<IOException>(() => busyProvider.PulseAsync(busyController,
            TestContext.Current.CancellationToken));

        FakeHidDevice? denied = null;
        denied = new(0x057E, 0x2009, "USB#switch-denied", 64, 64, () =>
            new FakeHidStream(denied!) { ReadException = new UnauthorizedAccessException() });
        var deniedProvider = new NintendoSwitchProHidProvider(() => [denied], _ => { });
        var deniedController = Assert.Single(await deniedProvider.GetControllersAsync(
            TestContext.Current.CancellationToken));
        Assert.Contains("denied", deniedController.BatteryNote,
            StringComparison.OrdinalIgnoreCase);
    }
}
