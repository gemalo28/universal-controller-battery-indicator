using System.IO;
using ControllerBattery.Models;
using ControllerBattery.Interop;
using HidSharp;

namespace ControllerBattery.Providers;

/// <summary>
/// Reads the official Nintendo Switch Pro Controller protocol over USB or Bluetooth.
/// Nintendo reports five battery bands, so this provider returns a category instead
/// of inventing a percentage.
/// </summary>
public sealed class NintendoSwitchProHidProvider : IControllerProvider, IPowerOffControllerProvider,
    IAttentionPulseControllerProvider
{
    private const int NintendoVendorId = 0x057E;
    private const int SwitchProProductId = 0x2009;
    private const int ReadTimeoutMilliseconds = 140;
    private const int ReportsToInspect = 8;
    private const string BluetoothHidService = "{00001124-0000-1000-8000-00805f9b34fb}";

    public string Id => "nintendo-switch-pro-hid";

    public Task<IReadOnlyList<ControllerDevice>> GetControllersAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    public Task PowerOffAsync(ControllerDevice controller, CancellationToken cancellationToken = default) =>
        Task.Run(() => PowerOff(controller, cancellationToken), cancellationToken);

    public Task PulseAsync(ControllerDevice controller, CancellationToken cancellationToken = default) =>
        Task.Run(() => Pulse(controller, cancellationToken), cancellationToken);

    private IReadOnlyList<ControllerDevice> Scan(CancellationToken cancellationToken)
    {
        var devices = DeviceList.Local.GetHidDevices(NintendoVendorId, SwitchProProductId)
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
        var hardwareId = GetStableHardwareId(device);
        var isUsb = !IsBluetooth(device);
        BatteryObservation? battery = null;
        string? note = null;

        try
        {
            if (!device.TryOpen(out var stream))
            {
                note = "Controller is in use by another application";
            }
            else
            {
                using (stream)
                {
                    stream.ReadTimeout = ReadTimeoutMilliseconds;
                    TryEnableFullReportMode(stream, device, isUsb);
                    var buffer = new byte[Math.Max(device.GetMaxInputReportLength(), 64)];

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
                            // Idle and sleeping controllers may not answer this scan.
                        }
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            note = "Controller access was denied";
        }
        catch (IOException)
        {
            note = "No Nintendo battery report received";
        }

        note ??= battery?.Note ?? "Battery data is waiting for a Nintendo full input report";
        return new ControllerDevice(
            hardwareId, Id, GetProductName(device), "Nintendo", isUsb ? "USB" : "Bluetooth",
            null, battery?.Level ?? BatteryLevel.Unknown, battery?.IsCharging ?? false,
            DateTime.Now, battery is null ? note : battery.Note, hardwareId, !isUsb, true);
    }

    private static void PowerOff(ControllerDevice controller, CancellationToken cancellationToken)
    {
        var device = DeviceList.Local.GetHidDevices(NintendoVendorId, SwitchProProductId)
            .FirstOrDefault(candidate => GetStableHardwareId(candidate)
                .Equals(controller.Id, StringComparison.OrdinalIgnoreCase))
            ?? throw new IOException("The Switch Pro Controller is no longer connected.");
        if (!IsBluetooth(device))
        {
            throw new NotSupportedException("A USB-connected controller remains powered by its cable.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        WindowsBluetooth.Disconnect(device.GetSerialNumber());
    }

    private static void Pulse(ControllerDevice controller, CancellationToken cancellationToken)
    {
        var device = DeviceList.Local.GetHidDevices(NintendoVendorId, SwitchProProductId)
            .FirstOrDefault(candidate => GetStableHardwareId(candidate)
                .Equals(controller.Id, StringComparison.OrdinalIgnoreCase));
        if (device is null)
            throw new IOException("The Switch Pro Controller is no longer connected.");
        if (!device.TryOpen(out var stream))
            throw new IOException("The Switch Pro Controller is in use by another application. Close Steam Input or the game and try again.");

        using (stream)
        {
            stream.WriteTimeout = 600;
            var length = device.GetMaxOutputReportLength();
            if (length < 12)
                throw new IOException("The wired controller does not expose a writable vibration report.");

            // A newly opened wired HID stream must complete Nintendo's USB
            // handshake before it will accept subcommands or rumble reports.
            TryEnableFullReportMode(stream, device, !IsBluetooth(device));
            stream.Write(CreateSubcommandReport(length, 0x48, 0x01)); // Enable vibration.

            const int pulsePackets =
                ControllerIdentification.PulseDurationMilliseconds /
                ControllerIdentification.RumbleKeepAliveMilliseconds;
            for (var packet = 1; packet <= pulsePackets; packet++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stream.Write(CreateRumbleReport(length, (byte)packet, enabled: true));
                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(
                    ControllerIdentification.RumbleKeepAliveMilliseconds));
            }

            // Multiple neutral frames stop HD rumble reliably on Bluetooth.
            for (var packet = pulsePackets + 1; packet <= pulsePackets + 3; packet++)
            {
                stream.Write(CreateRumbleReport(length, (byte)packet, enabled: false));
            }
        }
    }

    internal static byte[] CreateRumbleReport(int length, byte packetNumber, bool enabled)
    {
        var report = new byte[length];
        report[0] = 0x10;
        report[1] = (byte)(packetNumber & 0x0F);
        ReadOnlySpan<byte> motor = enabled
            ? [0x00, 0x61, 0x40, 0x58] // 320/160 Hz, moderate amplitude.
            : [0x00, 0x01, 0x40, 0x40];
        motor.CopyTo(report.AsSpan(2, 4));
        motor.CopyTo(report.AsSpan(6, 4));
        return report;
    }

    internal static BatteryObservation? ParseBattery(ReadOnlySpan<byte> report)
    {
        // Full state and subcommand replies share the controller-state header.
        if (report.Length < 3 || report[0] is not (0x21 or 0x30 or 0x31))
        {
            return null;
        }

        var batteryAndConnection = report[2];
        var rawLevel = (batteryAndConnection & 0xE0) >> 5;
        var isCharging = (batteryAndConnection & 0x10) != 0;
        var level = rawLevel switch
        {
            0 => BatteryLevel.Empty,
            1 => BatteryLevel.Low,
            2 => BatteryLevel.Medium,
            3 => BatteryLevel.High,
            4 => BatteryLevel.Full,
            _ => BatteryLevel.Unknown
        };
        var note = level == BatteryLevel.Full
            ? isCharging ? "Fully charged; connected to power" : "Fully charged"
            : isCharging ? "Charging" : null;

        return new BatteryObservation(level, isCharging, note);
    }

    private static void TryEnableFullReportMode(HidStream stream, HidDevice device, bool isUsb)
    {
        try
        {
            if (isUsb)
            {
                WriteUsbCommand(stream, device, 0x02); // handshake
                WriteUsbCommand(stream, device, 0x03); // faster UART
                WriteUsbCommand(stream, device, 0x02); // handshake at new rate
                WriteUsbCommand(stream, device, 0x04); // disable USB timeout
            }

            var length = device.GetMaxOutputReportLength();
            if (length < 12) return;

            stream.Write(CreateSubcommandReport(length, 0x03, 0x30));
        }
        catch (IOException)
        {
            // Passive parsing can still work if another driver initialized it.
        }
        catch (TimeoutException)
        {
            // Best-effort initialization must not hide the controller.
        }
    }

    private static byte[] CreateSubcommandReport(int length, byte subcommand, byte argument)
    {
        var report = new byte[length];
        report[0] = 0x01;
        report[2] = 0x00; report[3] = 0x01; report[4] = 0x40; report[5] = 0x40;
        report[6] = 0x00; report[7] = 0x01; report[8] = 0x40; report[9] = 0x40;
        report[10] = subcommand;
        report[11] = argument;
        return report;
    }

    private static void WriteUsbCommand(HidStream stream, HidDevice device, byte command)
    {
        var length = device.GetMaxOutputReportLength();
        if (length < 2) return;

        var report = new byte[length];
        report[0] = 0x80;
        report[1] = command;
        stream.Write(report);
    }

    private static string GetProductName(HidDevice device)
    {
        try
        {
            var product = device.GetProductName();
            if (string.IsNullOrWhiteSpace(product))
            {
                return "Nintendo Switch Pro Controller";
            }

            var name = product.Trim();
            return name.Equals("Wireless Gamepad", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Gamepad", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Pro Controller", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Wireless Controller", StringComparison.OrdinalIgnoreCase)
                ? "Nintendo Switch Pro Controller"
                : name;
        }
        catch (IOException)
        {
            return "Nintendo Switch Pro Controller";
        }
    }

    private static bool IsBluetooth(HidDevice device) =>
        device.DevicePath.Contains("BTH", StringComparison.OrdinalIgnoreCase) ||
        device.DevicePath.Contains(BluetoothHidService, StringComparison.OrdinalIgnoreCase);

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
            // Serial numbers are optional over Bluetooth.
        }

        return $"{device.VendorID:X4}:{device.ProductID:X4}:{device.DevicePath}";
    }

    internal sealed record BatteryObservation(BatteryLevel Level, bool IsCharging, string? Note);
}
