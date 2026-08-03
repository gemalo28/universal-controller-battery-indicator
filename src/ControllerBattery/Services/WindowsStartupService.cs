using Microsoft.Win32;

namespace ControllerBattery.Services;

public static class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ControllerBattery";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new UnauthorizedAccessException("Windows startup settings could not be opened.");

        if (enabled)
            key.SetValue(ValueName, BuildCommand(Environment.ProcessPath), RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    internal static string BuildCommand(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException("The application executable path is unavailable.");

        return $"\"{executablePath}\" --background";
    }
}
