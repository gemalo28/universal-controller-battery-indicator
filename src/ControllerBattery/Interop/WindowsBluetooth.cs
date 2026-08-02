using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace ControllerBattery.Interop;

internal static class WindowsBluetooth
{
    private const uint IoctlDisconnectDevice = 0x41000C;

    internal static void Disconnect(string? serial)
    {
        if (!TryParseAddress(serial, out var address))
        {
            throw new IOException("Windows did not expose the controller's Bluetooth address.");
        }

        var parameters = new FindRadioParams { Size = Marshal.SizeOf<FindRadioParams>() };
        var findHandle = NativeMethods.BluetoothFindFirstRadio(ref parameters, out var radioHandle);
        if (findHandle == IntPtr.Zero)
        {
            throw new IOException("Windows could not find a Bluetooth radio.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        var error = 0;
        try
        {
            while (radioHandle != IntPtr.Zero)
            {
                try
                {
                    if (NativeMethods.DeviceIoControl(radioHandle, IoctlDisconnectDevice,
                            ref address, sizeof(long), IntPtr.Zero, 0, out _, IntPtr.Zero))
                    {
                        return;
                    }

                    error = Marshal.GetLastWin32Error();
                }
                finally
                {
                    NativeMethods.CloseHandle(radioHandle);
                }

                if (!NativeMethods.BluetoothFindNextRadio(findHandle, out radioHandle))
                    radioHandle = IntPtr.Zero;
            }
        }
        finally
        {
            NativeMethods.BluetoothFindRadioClose(findHandle);
        }

        throw new IOException("Windows could not disconnect the Bluetooth controller.",
            new Win32Exception(error));
    }

    private static bool TryParseAddress(string? serial, out long address)
    {
        address = 0;
        if (string.IsNullOrWhiteSpace(serial)) return false;
        var compact = serial.Replace(":", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal);
        if (compact.Length != 12) return false;

        try
        {
            var bytes = Convert.FromHexString(compact);
            Array.Reverse(bytes);
            var padded = new byte[8];
            bytes.CopyTo(padded, 0);
            address = BitConverter.ToInt64(padded, 0);
            return true;
        }
        catch (FormatException) { return false; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FindRadioParams { public int Size; }

    private static class NativeMethods
    {
        [DllImport("bthprops.cpl", SetLastError = true)]
        internal static extern IntPtr BluetoothFindFirstRadio(ref FindRadioParams parameters, out IntPtr radio);
        [DllImport("bthprops.cpl", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothFindNextRadio(IntPtr find, out IntPtr radio);
        [DllImport("bthprops.cpl", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothFindRadioClose(IntPtr find);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeviceIoControl(IntPtr handle, uint code, ref long input,
            int inputSize, IntPtr output, int outputSize, out int returned, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
