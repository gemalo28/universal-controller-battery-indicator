using ControllerBattery.Models;

namespace ControllerBattery.Services;

public static class ControllerProfileService
{
    private static readonly HashSet<string> AllowedIconKinds = new(StringComparer.OrdinalIgnoreCase)
        { "Xbox", "PlayStation", "Nintendo", "8BitDo", "Generic" };

    public static string DeviceKey(ControllerDevice controller) =>
        $"{controller.ProviderId}:{controller.Id}";

    public static IReadOnlyList<ControllerDevice> Apply(
        IReadOnlyList<ControllerDevice> controllers,
        IReadOnlyDictionary<string, ControllerProfile> profiles) =>
        controllers.Select(controller => profiles.TryGetValue(DeviceKey(controller), out var profile)
            ? controller with
            {
                Name = string.IsNullOrWhiteSpace(profile.CustomName) ? controller.Name : profile.CustomName.Trim(),
                AccentColor = profile.AccentColor,
                ProfileIconKind = profile.IconKind
            }
            : controller with { AccentColor = ControllerProfile.DefaultAccentColor }).ToArray();

    public static ControllerProfile Normalize(ControllerProfile profile)
    {
        var name = string.IsNullOrWhiteSpace(profile.CustomName) ? null : profile.CustomName.Trim();
        if (name?.Length > 60) name = name[..60];
        var color = NormalizeColor(profile.AccentColor) ?? ControllerProfile.DefaultAccentColor;
        var iconKind = string.IsNullOrWhiteSpace(profile.IconKind) ? null : profile.IconKind.Trim();
        if (iconKind is not null && !AllowedIconKinds.Contains(iconKind)) iconKind = null;
        var parent = string.IsNullOrWhiteSpace(profile.ParentDeviceKey) ? null : profile.ParentDeviceKey.Trim();
        if (parent?.Equals(profile.DeviceKey, StringComparison.OrdinalIgnoreCase) == true) parent = null;
        var ledColor = NormalizeColor(profile.LedColor);
        if (ledColor is null && profile.UseAccentForLed) ledColor = color;
        return profile with
        {
            CustomName = name,
            AccentColor = color,
            IconKind = iconKind,
            UseAccentForLed = false,
            LedColor = ledColor,
            LedBrightness = profile.LedBrightness > 2 ? (byte)0 : profile.LedBrightness,
            ParentDeviceKey = parent
        };
    }

    private static string? NormalizeColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 7 && value[0] == '#' &&
        value[1..].All(Uri.IsHexDigit) ? value.ToUpperInvariant() : null;
}
