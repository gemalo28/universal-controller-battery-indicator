using HidSharp;
using ControllerBattery.Models;
using System.IO;

namespace ControllerBattery.Providers;

/// <summary>
/// Discovers Sony DualSense-family controllers exposed directly through HID.
/// Supports the standard DualSense and DualSense Edge over USB and Bluetooth.
/// </summary>
public sealed class DualSenseHidProvider : IControllerProvider, IPowerOffControllerProvider,
    IAttentionPulseControllerProvider
{
    private const int SonyVendorId = 0x054C;
    private const int DualSenseProductId = 0x0CE6;
    private const int DualSenseEdgeProductId = 0x0DF2;
    private const int ReadTimeoutMilliseconds = 180;
    private const int ReportsToInspect = 6;

    public string Id => "sony-dualsense-hid";

    public Task<IReadOnlyList<ControllerDevice>> GetControllersAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    public Task PowerOffAsync(ControllerDevice controller, CancellationToken cancellationToken = default) =>
        Task.Run(() => PowerOff(controller, cancellationToken), cancellationToken);

    public Task PulseAsync(ControllerDevice controller, CancellationToken cancellationToken = default) =>
        Task.Run(() => Pulse(controller, cancellationToken), cancellationToken);

    private IReadOnlyList<ControllerDevice> Scan(CancellationToken cancellationToken)
    {
        var devices = DeviceList.Local.GetHidDevices(SonyVendorId)
            .Where(IsSupportedProduct)
            .GroupBy(device => device.DevicePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

        var controllers = new List<ControllerDevice>();
        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            controllers.Add(ReadController(device, cancellationToken));
        }

        return controllers;
    }

    private ControllerDevice ReadController(HidDevice device, CancellationToken cancellationToken)
    {
        var modelName = device.ProductID == DualSenseEdgeProductId
            ? "DualSense Edge Wireless Controller"
            : "DualSense Wireless Controller";
        var connection = device.GetMaxInputReportLength() >= 78 ? "Bluetooth" : "USB";
        var hardwareId = GetStableHardwareId(device);
        BatteryObservation? battery = null;
        string? note = null;

        try
        {
            if (device.TryOpen(out var stream))
            {
                using (stream)
                {
                    stream.ReadTimeout = ReadTimeoutMilliseconds;
                    if (connection == "Bluetooth")
                    {
                        TryEnableFullBluetoothReports(stream, device);
                    }

                    var buffer = new byte[Math.Max(device.GetMaxInputReportLength(), 78)];

                    for (var attempt = 0; attempt < ReportsToInspect && battery is null; attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var bytesRead = stream.Read(buffer, 0, buffer.Length);
                            battery = ParseBattery(buffer.AsSpan(0, bytesRead));
                        }
                        catch (TimeoutException)
                        {
                            // A sleeping controller may not send a report during this short scan.
                        }
                    }
                }
            }
            else
            {
                note = "Controller is in use by another application";
            }
        }
        catch (UnauthorizedAccessException)
        {
            note = "Controller access was denied";
        }
        catch (IOException)
        {
            note = "No battery report received";
        }

        note ??= battery?.Note ?? "Battery data is waiting for a full HID report";

        return new ControllerDevice(
            hardwareId,
            Id,
            modelName,
            "PlayStation",
            connection,
            battery?.Percent,
            BatteryLevelClassifier.FromPercentage(battery?.Percent),
            battery?.IsCharging ?? false,
            DateTime.Now,
            battery?.Percent is null ? note : battery.Note,
            hardwareId,
            connection == "Bluetooth",
            true);
    }

    private static void TryEnableFullBluetoothReports(HidStream stream, HidDevice device)
    {
        try
        {
            // A Bluetooth DualSense initially emits the minimal 0x01 report, which
            // has no battery status. GET_FEATURE 0x05 (calibration) asks the
            // controller to switch to its full 0x31 input report. Windows requires
            // the collection-wide maximum feature buffer even though this specific
            // feature's protocol payload is only 41 bytes.
            var length = device.GetMaxFeatureReportLength();
            if (length <= 0) return;

            var calibration = new byte[length];
            calibration[0] = 0x05;
            stream.GetFeature(calibration);
        }
        catch (IOException)
        {
            // If another driver already initialized the controller, full reports
            // can still arrive and the normal parser below will consume them.
        }
    }

    private static void PowerOff(ControllerDevice controller, CancellationToken cancellationToken)
    {
        var device = DeviceList.Local.GetHidDevices(SonyVendorId)
            .Where(IsSupportedProduct)
            .FirstOrDefault(candidate => GetStableHardwareId(candidate)
                .Equals(controller.Id, StringComparison.OrdinalIgnoreCase))
            ?? throw new IOException("The DualSense controller is no longer connected.");
        if (device.GetMaxInputReportLength() < 78)
        {
            throw new NotSupportedException("A USB-connected DualSense remains powered by its cable.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        WindowsBluetooth.Disconnect(device.GetSerialNumber());
    }

    private static void Pulse(ControllerDevice controller, CancellationToken cancellationToken)
    {
        var device = DeviceList.Local.GetHidDevices(SonyVendorId)
            .Where(IsSupportedProduct)
            .FirstOrDefault(candidate => GetStableHardwareId(candidate)
                .Equals(controller.Id, StringComparison.OrdinalIgnoreCase));
        if (device is null)
            throw new IOException("The DualSense controller is no longer connected.");
        if (!device.TryOpen(out var stream))
            throw new IOException("The DualSense controller is in use by another application. Close Steam Input or the game and try again.");

        using (stream)
        {
            stream.WriteTimeout = 250;
            var bluetooth = device.GetMaxInputReportLength() >= 78;
            byte sequence = 0;
            try
            {
                for (var elapsed = 0;
                     elapsed < ControllerIdentification.PulseDurationMilliseconds;
                     elapsed += ControllerIdentification.RumbleKeepAliveMilliseconds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Write(CreateRumbleReport(bluetooth, sequence++, 105, 135));
                    cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(
                        ControllerIdentification.RumbleKeepAliveMilliseconds));
                }
            }
            finally
            {
                stream.Write(CreateRumbleReport(bluetooth, sequence, 0, 0));
            }
        }
    }

    internal static byte[] CreateRumbleReport(
        bool bluetooth, byte sequence, byte motorLeft, byte motorRight)
    {
        if (!bluetooth)
        {
            var report = new byte[63];
            report[0] = 0x02;
            report[1] = 0x03; // Select and enable compatible vibration.
            report[3] = motorRight;
            report[4] = motorLeft;
            return report;
        }

        var bluetoothReport = new byte[78];
        bluetoothReport[0] = 0x31;
        bluetoothReport[1] = (byte)((sequence & 0x0F) << 4);
        bluetoothReport[2] = 0x10;
        bluetoothReport[3] = 0x03;
        bluetoothReport[5] = motorRight;
        bluetoothReport[6] = motorLeft;

        var crc = ComputeOutputCrc(bluetoothReport.AsSpan(0, 74));
        BitConverter.TryWriteBytes(bluetoothReport.AsSpan(74, 4), crc);
        return bluetoothReport;
    }

    private static uint ComputeOutputCrc(ReadOnlySpan<byte> report)
    {
        var crc = UpdateCrc(0xFFFFFFFF, 0xA2);
        foreach (var value in report) crc = UpdateCrc(crc, value);
        return ~crc;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        return crc;
    }

    internal static BatteryObservation? ParseBattery(ReadOnlySpan<byte> report)
    {
        // USB: report ID 0x01, 64 bytes, common payload begins at byte 1.
        // Bluetooth: report ID 0x31, 78 bytes, common payload begins at byte 2.
        var statusIndex = report switch
        {
            { Length: >= 64 } when report[0] == 0x01 => 53,
            { Length: >= 78 } when report[0] == 0x31 => 54,
            _ => -1
        };

        if (statusIndex < 0 || statusIndex >= report.Length)
        {
            return null;
        }

        var status = report[statusIndex];
        var level = status & 0x0F;
        var chargingState = (status >> 4) & 0x0F;

        return chargingState switch
        {
            0x0 => new BatteryObservation(Math.Min(level * 10 + 5, 100), false, null),
            0x1 => new BatteryObservation(Math.Min(level * 10 + 5, 100), true, null),
            0x2 => new BatteryObservation(100, false, "Fully charged"),
            0xA => new BatteryObservation(null, false, "Not charging: voltage or temperature out of range"),
            0xB => new BatteryObservation(null, false, "Not charging: temperature error"),
            0xF => new BatteryObservation(null, false, "Charging error"),
            _ => new BatteryObservation(null, false, "Unknown charging state")
        };
    }

    private static bool IsSupportedProduct(HidDevice device) =>
        device.ProductID is DualSenseProductId or DualSenseEdgeProductId;

    private static string GetStableHardwareId(HidDevice device)
    {
        try
        {
            var serial = device.GetSerialNumber();
            if (!string.IsNullOrWhiteSpace(serial))
            {
                return $"{device.VendorID:X4}:{device.ProductID:X4}:{serial}";
            }
        }
        catch (IOException)
        {
            // Bluetooth stacks and busy devices do not always expose serial data.
        }

        return $"{device.VendorID:X4}:{device.ProductID:X4}:{device.DevicePath}";
    }

    internal sealed record BatteryObservation(int? Percent, bool IsCharging, string? Note);
}
