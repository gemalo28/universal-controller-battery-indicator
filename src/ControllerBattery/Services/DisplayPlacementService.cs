using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ControllerBattery.Models;

namespace ControllerBattery.Services;

internal static class DisplayPlacementService
{
    private const uint MonitorDefaultToNearest = 2;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    internal static void PositionTopmost(Window window, OverlayPosition position, int margin = 18)
    {
        var foreground = GetForegroundWindow();
        var monitor = FindFullscreenMonitor();
        var fullscreenDetected = monitor != IntPtr.Zero;
        if (!fullscreenDetected)
        {
            monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
        }
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var area = fullscreenDetected || IsFullscreen(foreground, info.Monitor)
            ? info.Monitor
            : info.WorkArea;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var windowBounds)) return;

        var width = windowBounds.Right - windowBounds.Left;
        var height = windowBounds.Bottom - windowBounds.Top;
        var x = position is OverlayPosition.TopLeft or OverlayPosition.BottomLeft
            ? area.Left + margin
            : area.Right - width - margin;
        var y = position is OverlayPosition.TopLeft or OverlayPosition.TopRight
            ? area.Top + margin
            : area.Bottom - height - margin;

        SetWindowPos(handle, HwndTopmost, x, y, 0, 0,
            SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    private static IntPtr FindFullscreenMonitor()
    {
        var shellWindow = GetShellWindow();
        var currentProcessId = (uint)Environment.ProcessId;
        var result = IntPtr.Zero;

        EnumWindows((window, _) =>
        {
            if (window == shellWindow || !IsWindowVisible(window) || IsIconic(window) ||
                GetWindowTextLength(window) == 0)
            {
                return true;
            }

            GetWindowThreadProcessId(window, out var processId);
            if (processId == currentProcessId || IsWindowCloaked(window)) return true;

            var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info) &&
                IsFullscreen(window, info.Monitor))
            {
                result = monitor;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static bool IsWindowCloaked(IntPtr window)
    {
        const int dwmwaCloaked = 14;
        return DwmGetWindowAttribute(window, dwmwaCloaked, out var cloaked, sizeof(int)) == 0 &&
               cloaked != 0;
    }

    private static bool IsFullscreen(IntPtr window, NativeRect monitor)
    {
        if (window == IntPtr.Zero || !GetWindowRect(window, out var bounds)) return false;
        const int tolerance = 2;
        return Math.Abs(bounds.Left - monitor.Left) <= tolerance &&
               Math.Abs(bounds.Top - monitor.Top) <= tolerance &&
               Math.Abs(bounds.Right - monitor.Right) <= tolerance &&
               Math.Abs(bounds.Bottom - monitor.Bottom) <= tolerance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y,
        int width, int height, uint flags);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
