using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using HidSharp;

namespace ControllerBattery.Services;

internal static class ControllerDiagnosticsService
{
    private const int ReadTimeoutMilliseconds = 120;
    private const int ReportsPerDevice = 12;

    internal static Task<string> CaptureAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ControllerBattery", "diagnostics");
            var path = Path.Combine(directory,
                $"controller-inputs-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            return Capture(DeviceList.Local.GetHidDevices(), path, cancellationToken);
        }, cancellationToken);

    internal static string Capture(IEnumerable<HidDevice> sourceDevices, string path,
        CancellationToken cancellationToken)
    {
        var devices = new List<object>();
        foreach (var device in sourceDevices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsGameController(device)) continue;

            var reports = new List<string>();
            string? captureError = null;
            try
            {
                if (!device.TryOpen(out var stream))
                {
                    captureError = "Device is currently in use.";
                }
                else
                {
                    using (stream)
                    {
                        stream.ReadTimeout = ReadTimeoutMilliseconds;
                        var inputLength = TryGetLength(device.GetMaxInputReportLength);
                        if (inputLength <= 0)
                        {
                            captureError = "Input report length is unavailable.";
                        }
                        else
                        {
                            var buffer = new byte[inputLength];
                            for (var attempt = 0; attempt < ReportsPerDevice; attempt++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                try
                                {
                                    var length = stream.Read(buffer, 0, buffer.Length);
                                    reports.Add(Convert.ToHexString(buffer.AsSpan(0, length)));
                                }
                                catch (TimeoutException) { }
                            }
                        }
                    }
                }
            }
            catch (Exception exception) when (IsDeviceException(exception))
            {
                captureError = exception.Message;
            }

            devices.Add(new
            {
                vendorId = $"{device.VendorID:X4}",
                productId = $"{device.ProductID:X4}",
                productName = TryGet(device.GetProductName),
                serialNumber = TryGet(device.GetSerialNumber),
                devicePath = device.DevicePath,
                maxInputReportLength = TryGetLength(device.GetMaxInputReportLength),
                maxOutputReportLength = TryGetLength(device.GetMaxOutputReportLength),
                maxFeatureReportLength = TryGetLength(device.GetMaxFeatureReportLength),
                reportDescriptor = TryGetDescriptor(device),
                reports,
                captureError
            });
        }

        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("A diagnostics path must have a directory.", nameof(path));
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            capturedAtUtc = DateTime.UtcNow,
            machine = new
            {
                os = Environment.OSVersion.VersionString,
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            note = "Game-controller HID devices only; keyboards and mice are excluded.",
            devices
        }, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static bool IsGameController(HidDevice device)
    {
        if (device.DevicePath.EndsWith("\\kbd", StringComparison.OrdinalIgnoreCase) ||
            device.DevicePath.Contains("&MI_00", StringComparison.OrdinalIgnoreCase) &&
            device.DevicePath.Contains("KBD", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var descriptor = device.GetRawReportDescriptor();
            // Generic Desktop Mouse (0x02) and Keyboard (0x06) collections are
            // never eligible, even when their shared product name says controller.
            if (ContainsTopLevelUsage(descriptor, 0x02) || ContainsTopLevelUsage(descriptor, 0x06))
                return false;
            for (var index = 0; index <= descriptor.Length - 4; index++)
            {
                if (descriptor[index] == 0x05 && descriptor[index + 1] == 0x01 &&
                    descriptor[index + 2] == 0x09 && descriptor[index + 3] is 0x04 or 0x05)
                    return true;
            }
        }
        catch (Exception exception) when (IsDeviceException(exception)) { }

        // Some vendor HID stacks expose a usable controller interface but an
        // invalid descriptor. Fall back only to controller-specific names/path
        // markers; never include generic keyboard or mouse interfaces.
        var name = TryGet(device.GetProductName) ?? string.Empty;
        return device.DevicePath.Contains("&IG_", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("controller", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("gamepad", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("joy-con", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("8bitdo", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ContainsTopLevelUsage(ReadOnlySpan<byte> descriptor, byte usage)
    {
        for (var index = 0; index <= descriptor.Length - 4; index++)
        {
            if (descriptor[index] == 0x05 && descriptor[index + 1] == 0x01 &&
                descriptor[index + 2] == 0x09 && descriptor[index + 3] == usage)
                return true;
        }
        return false;
    }

    internal static string? TryGet(Func<string> getter)
    {
        try { return getter(); }
        catch (Exception exception) when (IsDeviceException(exception))
        {
            return null;
        }
    }

    private static string? TryGetDescriptor(HidDevice device)
    {
        try { return Convert.ToHexString(device.GetRawReportDescriptor()); }
        catch (Exception exception) when (IsDeviceException(exception)) { return null; }
    }

    internal static int TryGetLength(Func<int> getter)
    {
        try { return getter(); }
        catch (Exception exception) when (IsDeviceException(exception)) { return 0; }
    }

    internal static bool IsDeviceException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException or
            NotSupportedException or ArgumentException;
}
