using System.IO;
using HidSharp;
using ControllerBattery.Models;

namespace ControllerBattery.Providers;

/// <summary>
/// Discovers 8BitDo devices that expose a native game-controller HID interface.
/// XInput-mode receivers are intentionally left to XInputControllerProvider because
/// their companion HID interfaces do not expose a trustworthy battery value.
/// </summary>
public sealed class EightBitDoHidProvider : IControllerProvider
{
    private const int EightBitDoVendorId = 0x2DC8;
    private const int ReadTimeoutMilliseconds = 120;
    private const int ReportsToInspect = 5;
    private readonly Func<IEnumerable<HidDevice>> _getDevices;
    private readonly Func<HidDevice, bool> _isGameController;

    public EightBitDoHidProvider() : this(
        () => DeviceList.Local.GetHidDevices(EightBitDoVendorId), IsGameController) { }

    internal EightBitDoHidProvider(Func<IEnumerable<HidDevice>> getDevices,
        Func<HidDevice, bool>? isGameController = null)
    {
        _getDevices = getDevices;
        _isGameController = isGameController ?? IsGameController;
    }

    public string Id => "8bitdo-hid";

    public Task<IReadOnlyList<ControllerDevice>> GetControllersAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private IReadOnlyList<ControllerDevice> Scan(CancellationToken cancellationToken)
    {
        var controllers = new List<ControllerDevice>();

        foreach (var device in _getDevices())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_isGameController(device))
            {
                continue;
            }

            var battery = TryReadBattery(device, cancellationToken);
            var isXInputCompanion = device.DevicePath.Contains("&ig_", StringComparison.OrdinalIgnoreCase);

            // Windows creates an IG_ companion collection for XInput devices. Keep
            // it only if it actually emits 8BitDo's enhanced report; otherwise the
            // XInput provider already owns the controller and this would duplicate it.
            if (isXInputCompanion && battery is null)
            {
                continue;
            }

            var hardwareId = GetStableHardwareId(device);
            var connection = GetConnection(device.DevicePath);
            controllers.Add(new ControllerDevice(
                hardwareId,
                Id,
                GetProductName(device),
                "8BitDo",
                connection,
                battery?.Percent,
                BatteryLevelClassifier.FromPercentage(battery?.Percent),
                battery?.IsCharging ?? false,
                DateTime.Now,
                battery?.Note ?? GetBatteryNote(connection),
                hardwareId));
        }

        return controllers
            .GroupBy(controller => controller.HardwareId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static BatteryObservation? TryReadBattery(
        HidDevice device,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!device.TryOpen(out var stream))
            {
                return null;
            }

            using (stream)
            {
                stream.ReadTimeout = ReadTimeoutMilliseconds;
                var buffer = new byte[Math.Max(device.GetMaxInputReportLength(), 34)];
                for (var attempt = 0; attempt < ReportsToInspect; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (ParseBattery(buffer.AsSpan(0, bytesRead)) is { } battery)
                        {
                            return battery;
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Sleeping and idle controllers may not emit within this scan.
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return null;
        }

        return null;
    }

    internal static BatteryObservation? ParseBattery(ReadOnlySpan<byte> report)
    {
        // 8BitDo enhanced mode report IDs: 0x01 over Bluetooth, 0x04 over
        // USB/receiver, and 0x03 on firmware without enhanced-mode negotiation.
        if (report.Length < 15 || report[0] is not (0x01 or 0x03 or 0x04))
        {
            return null;
        }

        var power = report[14];
        var level = power & 0x7F;
        if (level > 100)
        {
            return null;
        }

        var charging = (power & 0x80) != 0;
        return new BatteryObservation(
            level,
            charging,
            level == 100 ? "Fully charged" : null);
    }

    private static bool IsGameController(HidDevice device)
    {
        try
        {
            var descriptor = device.GetRawReportDescriptor();
            return ContainsUsage(descriptor, 0x05) || ContainsUsage(descriptor, 0x04);
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool ContainsUsage(ReadOnlySpan<byte> descriptor, byte usage)
    {
        // Generic Desktop usage page (05 01), followed by Game Pad (09 05)
        // or Joystick (09 04). This filters out receiver consumer-control and
        // vendor-maintenance collections without maintaining a product-ID list.
        for (var index = 0; index <= descriptor.Length - 4; index++)
        {
            if (descriptor[index] == 0x05 && descriptor[index + 1] == 0x01 &&
                descriptor[index + 2] == 0x09 && descriptor[index + 3] == usage)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetProductName(HidDevice device)
    {
        try
        {
            return NormalizeProductName(device.GetProductName());
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return "8BitDo Controller";
        }
    }

    internal static string NormalizeProductName(string? product) =>
        string.IsNullOrWhiteSpace(product) ? "8BitDo Controller" : product.Trim();

    private static string GetStableHardwareId(HidDevice device)
    {
        return BuildHardwareId(device.VendorID, device.ProductID, GetSerialNumber(device),
            device.DevicePath);
    }

    internal static string BuildHardwareId(int vendorId, int productId, string? serial,
        string devicePath) => string.IsNullOrWhiteSpace(serial)
        ? $"{vendorId:X4}:{productId:X4}:{devicePath}"
        : $"{vendorId:X4}:{productId:X4}:{serial}";

    private static string? GetSerialNumber(HidDevice device)
    {
        try { return device.GetSerialNumber(); }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return null;
        }
    }

    internal static string GetConnection(string devicePath) =>
        devicePath.Contains("BTH", StringComparison.OrdinalIgnoreCase)
            ? "Bluetooth"
            : "USB / 2.4 GHz";

    internal static string GetBatteryNote(string connection) => connection == "Bluetooth"
        ? "Battery is not exposed through this HID mode"
        : "Battery is not exposed by this controller or receiver mode";

    internal sealed record BatteryObservation(int Percent, bool IsCharging, string? Note);
}
