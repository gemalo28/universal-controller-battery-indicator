using System.IO;
using ControllerBattery.Models;

namespace ControllerBattery.Services;

public static class AppSettingsStore
{
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControllerBattery", "settings.json");

    public static AppSettings Load() => LoadFrom(SettingsPath);

    internal static AppSettings LoadFrom(string path)
    {
        var settings = AtomicJsonFile.Load<AppSettings>(path) ?? AppSettings.Default;
        return AppSettingsService.Normalize(settings);
    }

    public static void Save(AppSettings settings) => SaveTo(SettingsPath, settings);
    internal static void SaveTo(string path, AppSettings settings) => AtomicJsonFile.Save(path, settings);
}
