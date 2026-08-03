using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace ControllerBattery.Interop;

internal static class WindowsBluetooth
{
    private const uint IoctlDisconnectDevice = 0x41000C;

    internal static void Disconnect(string? serial)
        => Disconnect(serial, WindowsBluetoothNative.Instance);

    internal static void Disconnect(string? serial, IWindowsBluetoothNative native)
    {
        if (!TryParseAddress(serial, out var address))
        {
            throw new IOException("Windows did not expose the controller's Bluetooth address.");
        }

        var findHandle = native.FindFirstRadio(out var radioHandle);
        if (findHandle == IntPtr.Zero)
        {
            throw new IOException("Windows could not find a Bluetooth radio.",
                new Win32Exception(native.LastError));
        }

        var error = 0;
        try
        {
            while (radioHandle != IntPtr.Zero)
            {
                try
                {
                    if (native.DisconnectDevice(radioHandle, IoctlDisconnectDevice, ref address))
                    {
                        return;
                    }

                    error = native.LastError;
                }
                finally
                {
                    native.CloseHandle(radioHandle);
                }

                if (!native.FindNextRadio(findHandle, out radioHandle))
                    radioHandle = IntPtr.Zero;
            }
        }
        finally
        {
            native.CloseFind(findHandle);
        }

        throw new IOException("Windows could not disconnect the Bluetooth controller.",
            new Win32Exception(error));
    }

    internal static bool TryParseAddress(string? serial, out long address)
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

    internal interface IWindowsBluetoothNative
    {
        int LastError { get; }
        IntPtr FindFirstRadio(out IntPtr radio);
        bool FindNextRadio(IntPtr find, out IntPtr radio);
        bool DisconnectDevice(IntPtr handle, uint code, ref long address);
        void CloseHandle(IntPtr handle);
        void CloseFind(IntPtr find);
    }

    private sealed class WindowsBluetoothNative : IWindowsBluetoothNative
    {
        internal static readonly WindowsBluetoothNative Instance = new();
        public int LastError => Marshal.GetLastWin32Error();

        public IntPtr FindFirstRadio(out IntPtr radio)
        {
            var parameters = new FindRadioParams { Size = Marshal.SizeOf<FindRadioParams>() };
            return NativeMethods.BluetoothFindFirstRadio(ref parameters, out radio);
        }

        public bool FindNextRadio(IntPtr find, out IntPtr radio) =>
            NativeMethods.BluetoothFindNextRadio(find, out radio);

        public bool DisconnectDevice(IntPtr handle, uint code, ref long address) =>
            NativeMethods.DeviceIoControl(handle, code, ref address, sizeof(long), IntPtr.Zero, 0,
                out _, IntPtr.Zero);

        public void CloseHandle(IntPtr handle) => NativeMethods.CloseHandle(handle);
        public void CloseFind(IntPtr find) => NativeMethods.BluetoothFindRadioClose(find);
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
