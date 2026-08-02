using System.IO;
using System.Text.Json;
using ControllerBattery.Models;

namespace ControllerBattery.Services;

public static class ControllerProfileStore
{
    private static readonly HashSet<string> AllowedIconKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Xbox", "PlayStation", "Nintendo", "8BitDo", "Generic"
    };

    private static readonly string ProfilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControllerBattery", "controller-profiles.json");

    public static Dictionary<string, ControllerProfile> Load()
    {
        try
        {
            var profiles = File.Exists(ProfilesPath)
                ? JsonSerializer.Deserialize<List<ControllerProfile>>(File.ReadAllText(ProfilesPath)) ?? []
                : [];
            return profiles
                .Where(profile => !string.IsNullOrWhiteSpace(profile.DeviceKey))
                .Select(Normalize)
                .GroupBy(profile => profile.DeviceKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { return new(StringComparer.OrdinalIgnoreCase); }
        catch (IOException) { return new(StringComparer.OrdinalIgnoreCase); }
    }

    public static void Save(IReadOnlyDictionary<string, ControllerProfile> profiles)
    {
        var directory = Path.GetDirectoryName(ProfilesPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(ProfilesPath, JsonSerializer.Serialize(profiles.Values.OrderBy(p => p.DeviceKey),
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static ControllerProfile Normalize(ControllerProfile profile)
    {
        var name = string.IsNullOrWhiteSpace(profile.CustomName) ? null : profile.CustomName.Trim();
        if (name?.Length > 60)
            name = name[..60];

        var color = profile.AccentColor;
        if (string.IsNullOrWhiteSpace(color) || color.Length != 7 || color[0] != '#' ||
            !color[1..].All(Uri.IsHexDigit))
        {
            color = ControllerProfile.DefaultAccentColor;
        }

        var iconKind = string.IsNullOrWhiteSpace(profile.IconKind) ? null : profile.IconKind.Trim();
        if (iconKind is not null && !AllowedIconKinds.Contains(iconKind))
            iconKind = null;

        return profile with
        {
            CustomName = name,
            AccentColor = color.ToUpperInvariant(),
            IconKind = iconKind
        };
    }
}
