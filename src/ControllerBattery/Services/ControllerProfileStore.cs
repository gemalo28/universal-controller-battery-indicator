using System.IO;
using ControllerBattery.Models;

namespace ControllerBattery.Services;

public static class ControllerProfileStore
{
    private static readonly string ProfilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControllerBattery", "controller-profiles.json");

    public static Dictionary<string, ControllerProfile> Load() => LoadFrom(ProfilesPath);

    internal static Dictionary<string, ControllerProfile> LoadFrom(string path)
    {
        try
        {
            var profiles = AtomicJsonFile.Load<List<ControllerProfile>>(path) ?? [];
            return profiles
                .Where(profile => !string.IsNullOrWhiteSpace(profile.DeviceKey))
                .Select(ControllerProfileService.Normalize)
                .GroupBy(profile => profile.DeviceKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException) { return new(StringComparer.OrdinalIgnoreCase); }
        catch (UnauthorizedAccessException) { return new(StringComparer.OrdinalIgnoreCase); }
    }

    public static void Save(IReadOnlyDictionary<string, ControllerProfile> profiles) =>
        SaveTo(ProfilesPath, profiles);

    internal static void SaveTo(string path, IReadOnlyDictionary<string, ControllerProfile> profiles) =>
        AtomicJsonFile.Save(path, profiles.Values.OrderBy(p => p.DeviceKey));

}
