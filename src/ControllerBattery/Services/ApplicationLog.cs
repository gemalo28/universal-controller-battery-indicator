using System.Diagnostics;
using System.IO;

namespace ControllerBattery.Services;

internal static class ApplicationLog
{
    private static readonly object Sync = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControllerBattery", "controller-battery.log");

    internal static void Write(string message)
    {
        Trace.WriteLine(message);
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"Unable to write application log: {exception}");
        }
    }
}
