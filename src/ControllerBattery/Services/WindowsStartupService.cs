using Microsoft.Win32;

namespace ControllerBattery.Services;

public static class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ControllerBattery";

    public static void SetEnabled(bool enabled) =>
        SetEnabled(enabled, new WindowsStartupRegistry(), Environment.ProcessPath);

    internal static void SetEnabled(bool enabled, IWindowsStartupRegistry registry,
        string? executablePath)
    {
        if (enabled)
            registry.SetValue(BuildCommand(executablePath));
        else
            registry.DeleteValue();
    }

    internal static string BuildCommand(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException("The application executable path is unavailable.");

        return $"\"{executablePath}\" --background";
    }

    internal interface IWindowsStartupRegistry
    {
        void SetValue(string command);
        void DeleteValue();
    }

    private sealed class WindowsStartupRegistry : IWindowsStartupRegistry
    {
        public void SetValue(string command)
        {
            using var key = OpenKey();
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }

        public void DeleteValue()
        {
            using var key = OpenKey();
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        private static RegistryKey OpenKey() =>
            Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new UnauthorizedAccessException("Windows startup settings could not be opened.");
    }
}
