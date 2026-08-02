using System.IO;
using System.Text.Json;
using ControllerBattery.Models;

namespace ControllerBattery.Services;

public static class AppSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControllerBattery", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var settings = File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? AppSettings.Default
                : AppSettings.Default;
            // Migrate the original default, which conflicts with shortcuts used by
            // some games. User-selected shortcuts other than that legacy value are preserved.
            if (settings.OverlayModifiers ==
                    (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift) &&
                settings.OverlayKey == System.Windows.Input.Key.B)
            {
                settings = settings with
                {
                    OverlayModifiers = AppSettings.Default.OverlayModifiers,
                    OverlayKey = AppSettings.Default.OverlayKey
                };
            }
            return settings.PollingIntervalSeconds is >= AppSettings.MinimumPollingIntervalSeconds
                and <= AppSettings.MaximumPollingIntervalSeconds
                ? settings
                : settings with { PollingIntervalSeconds = AppSettings.DefaultPollingIntervalSeconds };
        }
        catch (JsonException) { return AppSettings.Default; }
        catch (IOException) { return AppSettings.Default; }
    }

    public static void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}
