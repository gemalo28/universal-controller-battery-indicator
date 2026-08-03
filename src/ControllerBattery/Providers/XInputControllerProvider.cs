using System.IO;
using System.Runtime.InteropServices;
using ControllerBattery.Models;

namespace ControllerBattery.Providers;

/// <summary>
/// Discovers controllers exposed through XInput, including adapters that emulate
/// an Xbox 360 controller. XInput exposes four coarse battery levels and some
/// adapters identify as wired, in which case no battery level is available.
/// </summary>
public sealed class XInputControllerProvider : IControllerProvider, IAttentionPulseControllerProvider
{
    private const uint ErrorSuccess = 0;
    private const byte BatteryDeviceTypeGamepad = 0;
    private const byte BatteryTypeDisconnected = 0;
    private const byte BatteryTypeWired = 1;
    private const byte BatteryTypeAlkaline = 2;
    private const byte BatteryTypeNimh = 3;

    public string Id => "xinput";

    public Task<IReadOnlyList<ControllerDevice>> GetControllersAsync(
        CancellationToken cancellationToken = default)
    {
        var controllers = new List<ControllerDevice>();

        for (uint userIndex = 0; userIndex < 4; userIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (NativeMethods.XInputGetState(userIndex, out _) != ErrorSuccess)
            {
                continue;
            }

            var batteryResult = NativeMethods.XInputGetBatteryInformation(
                userIndex, BatteryDeviceTypeGamepad, out var battery);
            var batteryAvailable = batteryResult == ErrorSuccess &&
                                   battery.BatteryType is BatteryTypeAlkaline or BatteryTypeNimh;

            controllers.Add(new ControllerDevice(
                $"xinput-{userIndex}",
                Id,
                $"XInput Controller {userIndex + 1}",
                "Xbox-compatible",
                battery.BatteryType == BatteryTypeWired ? "USB adapter" : "Wireless",
                null,
                batteryAvailable ? ToBatteryLevel(battery.BatteryLevel) : BatteryLevel.Unknown,
                false,
                DateTime.Now,
                batteryAvailable ? null : "Battery level is not exposed by the adapter",
                CanIdentify: true));
        }

        return Task.FromResult<IReadOnlyList<ControllerDevice>>(controllers);
    }

    public async Task PulseAsync(
        ControllerDevice controller,
        CancellationToken cancellationToken = default)
    {
        if (!controller.Id.StartsWith("xinput-", StringComparison.OrdinalIgnoreCase) ||
            !uint.TryParse(controller.Id[7..], out var userIndex) || userIndex >= 4)
        {
            throw new IOException("The XInput controller identifier is invalid.");
        }

        var vibration = new XInputVibration
        {
            LeftMotorSpeed = 28000,
            RightMotorSpeed = 42000
        };
        if (NativeMethods.XInputSetState(userIndex, ref vibration) != ErrorSuccess)
            throw new IOException("The wired controller is no longer available.");

        try
        {
            for (var elapsed = 0;
                 elapsed < ControllerIdentification.PulseDurationMilliseconds;
                 elapsed += ControllerIdentification.RumbleKeepAliveMilliseconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // ViGEm only raises a feedback event when the virtual motor state
                // changes. Alternate by an imperceptible amount so translators such
                // as BetterJoy receive every keepalive instead of coalescing them.
                vibration.RightMotorSpeed = (ushort)(42000 -
                    ((elapsed / ControllerIdentification.RumbleKeepAliveMilliseconds) % 2) * 16);
                if (NativeMethods.XInputSetState(userIndex, ref vibration) != ErrorSuccess)
                    throw new IOException("The wired controller is no longer available.");
                await Task.Delay(ControllerIdentification.RumbleKeepAliveMilliseconds,
                    cancellationToken);
            }
        }
        finally
        {
            vibration = default;
            NativeMethods.XInputSetState(userIndex, ref vibration);
        }
    }

    internal static BatteryLevel ToBatteryLevel(byte level) => level switch
    {
        0 => BatteryLevel.Empty,
        1 => BatteryLevel.Low,
        2 => BatteryLevel.Medium,
        3 => BatteryLevel.Full,
        _ => BatteryLevel.Unknown
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputBatteryInformation
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputVibration
    {
        public ushort LeftMotorSpeed;
        public ushort RightMotorSpeed;
    }

    private static class NativeMethods
    {
        [DllImport("xinput1_4.dll", ExactSpelling = true)]
        internal static extern uint XInputGetState(uint userIndex, out XInputState state);

        [DllImport("xinput1_4.dll", ExactSpelling = true)]
        internal static extern uint XInputGetBatteryInformation(
            uint userIndex,
            byte deviceType,
            out XInputBatteryInformation batteryInformation);

        [DllImport("xinput1_4.dll", ExactSpelling = true)]
        internal static extern uint XInputSetState(uint userIndex, ref XInputVibration vibration);
    }
}
