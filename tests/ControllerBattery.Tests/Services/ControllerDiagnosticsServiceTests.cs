using System.Text.Json;
using ControllerBattery.Services;
using ControllerBattery.Tests.Fakes;

namespace ControllerBattery.Tests.Services;

public sealed class ControllerDiagnosticsServiceTests
{
    [Fact]
    public void Capture_WritesStructuredEmptyCapture()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "capture.json");
        var result = ControllerDiagnosticsService.Capture([], path,
            TestContext.Current.CancellationToken);
        Assert.Equal(path, result);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("devices").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("devices").GetArrayLength());
    }

    [Fact]
    public void Capture_RecordsInjectedControllerReports()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "controller.json");
        FakeHidDevice? device = null;
        device = new(0x1234, 0x5678, "HID&IG_GAMEPAD", 16, 16,
            () => new FakeHidStream(device!, [[0x01, 0x02, 0x03]]));
        ControllerDiagnosticsService.Capture([device], path,
            TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var captured = Assert.Single(json.RootElement.GetProperty("devices").EnumerateArray());
        Assert.Equal("1234", captured.GetProperty("vendorId").GetString());
        Assert.Contains("010203", captured.GetProperty("reports").EnumerateArray()
            .Select(value => value.GetString()));
    }

    [Fact]
    public void Capture_RecordsBusyMissingLengthAndReadFailureStates()
    {
        using var directory = new TemporaryDirectory();
        FakeHidDevice? busy = null;
        busy = new(1, 1, "HID&IG_BUSY", 16, 16,
            () => throw new IOException("busy"));
        FakeHidDevice? noLength = null;
        noLength = new(2, 2, "HID&IG_NOLENGTH", 0, 16,
            () => new FakeHidStream(noLength!));
        FakeHidDevice? readFailure = null;
        readFailure = new(3, 3, "HID&IG_READ", 16, 16,
            () => new FakeHidStream(readFailure!) { ReadException = new IOException("read failed") });

        var path = Path.Combine(directory.Path, "errors.json");
        ControllerDiagnosticsService.Capture([busy, noLength, readFailure], path,
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var errors = json.RootElement.GetProperty("devices").EnumerateArray()
            .Select(device => device.GetProperty("captureError").GetString()).ToArray();
        Assert.Contains(errors, error => error is not null && error.Contains("use"));
        Assert.Contains("Input report length is unavailable.", errors);
        Assert.Contains("read failed", errors);
    }

    [Fact]
    public void Capture_FiltersKeyboardAndMouseButFallsBackForKnownControllerPaths()
    {
        using var directory = new TemporaryDirectory();
        FakeHidDevice? keyboard = null;
        keyboard = new(1, 1, "HID#device\\kbd", 8, 8,
            () => new FakeHidStream(keyboard!));
        FakeHidDevice? mouse = null;
        mouse = new(2, 2, "HID#mouse", 8, 8, () => new FakeHidStream(mouse!),
            descriptor: [0x05, 0x01, 0x09, 0x02]);
        FakeHidDevice? fallback = null;
        fallback = new(3, 3, "HID&IG_FALLBACK", 8, 8,
            () => new FakeHidStream(fallback!))
        { DescriptorException = new IOException("descriptor") };

        var path = Path.Combine(directory.Path, "filtered.json");
        ControllerDiagnosticsService.Capture([keyboard, mouse, fallback], path,
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var captured = Assert.Single(json.RootElement.GetProperty("devices").EnumerateArray());
        Assert.Contains("FALLBACK", captured.GetProperty("devicePath").GetString());
    }

    [Theory]
    [InlineData(0x04, true)]
    [InlineData(0x05, true)]
    [InlineData(0x02, false)]
    public void ContainsTopLevelUsage_ClassifiesDescriptors(byte usage, bool expected)
    {
        byte[] descriptor = [0x05, 0x01, 0x09, usage];
        Assert.Equal(expected,
            ControllerDiagnosticsService.ContainsTopLevelUsage(descriptor, 0x04) ||
            ControllerDiagnosticsService.ContainsTopLevelUsage(descriptor, 0x05));
    }

    [Fact]
    public void SafeGetterHelpers_HandleSupportedDeviceExceptions()
    {
        Assert.Equal("value", ControllerDiagnosticsService.TryGet(() => "value"));
        Assert.Null(ControllerDiagnosticsService.TryGet(() => throw new IOException("busy")));
        Assert.Equal(42, ControllerDiagnosticsService.TryGetLength(() => 42));
        Assert.Equal(0, ControllerDiagnosticsService.TryGetLength(() =>
            throw new NotSupportedException()));
    }

    [Theory]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(NotSupportedException), true)]
    [InlineData(typeof(ArgumentException), true)]
    [InlineData(typeof(FormatException), false)]
    public void DeviceExceptionClassification_IsExplicit(Type exceptionType, bool expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        Assert.Equal(expected, ControllerDiagnosticsService.IsDeviceException(exception));
    }
}
