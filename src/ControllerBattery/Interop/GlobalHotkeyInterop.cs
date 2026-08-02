using System.Runtime.InteropServices;

namespace ControllerBattery.Interop;

internal static class GlobalHotkeyInterop
{
    internal static bool Register(IntPtr window, int id, uint modifiers, uint virtualKey) =>
        RegisterHotKey(window, id, modifiers, virtualKey);
    internal static bool Unregister(IntPtr window, int id) => UnregisterHotKey(window, id);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
