using System.Runtime.InteropServices;

namespace ControllerBattery.Interop;

internal static class DeviceNotificationInterop
{
    internal const int WmDeviceChange = 0x0219;
    internal const int DeviceNodesChanged = 0x0007;
    internal const int DeviceArrival = 0x8000;
    internal const int DeviceRemoveComplete = 0x8004;

    private const int DeviceTypeInterface = 0x00000005;
    private const uint NotifyWindowHandle = 0x00000000;
    private static readonly Guid HidInterfaceClass = new("4D1E55B2-F16F-11CF-88CB-001111000030");
    private static readonly Guid XusbInterfaceClass = new("EC87F1E3-C13B-4100-B5F7-8B84D54260CB");

    internal static IReadOnlyList<IntPtr> RegisterControllerNotifications(IntPtr windowHandle)
    {
        var registrations = new List<IntPtr>(2);
        Register(windowHandle, HidInterfaceClass, registrations);
        Register(windowHandle, XusbInterfaceClass, registrations);
        return registrations;
    }

    internal static void UnregisterControllerNotifications(IEnumerable<IntPtr> registrations)
    {
        foreach (var registration in registrations)
        {
            if (registration != IntPtr.Zero)
                UnregisterDeviceNotification(registration);
        }
    }

    internal static bool IsControllerDeviceChange(int eventType) =>
        eventType is DeviceNodesChanged or DeviceArrival or DeviceRemoveComplete;

    private static void Register(IntPtr windowHandle, Guid interfaceClass,
        ICollection<IntPtr> registrations)
    {
        var filter = new DeviceBroadcastInterface
        {
            Size = Marshal.SizeOf<DeviceBroadcastInterface>(),
            DeviceType = DeviceTypeInterface,
            ClassGuid = interfaceClass
        };
        var registration = RegisterDeviceNotification(windowHandle, ref filter, NotifyWindowHandle);
        if (registration != IntPtr.Zero)
            registrations.Add(registration);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceBroadcastInterface
    {
        internal int Size;
        internal int DeviceType;
        internal int Reserved;
        internal Guid ClassGuid;
        internal char Name;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr RegisterDeviceNotification(IntPtr recipient,
        ref DeviceBroadcastInterface notificationFilter, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterDeviceNotification(IntPtr handle);
}
