using ControllerBattery.Models;

namespace ControllerBattery.Services;

public sealed record ControllerConnectionChange(ControllerDevice Controller, bool IsConnected);

public sealed class ControllerConnectionService
{
    private Dictionary<string, ControllerDevice>? _previous;

    public IReadOnlyList<ControllerConnectionChange> FindChanges(
        IReadOnlyList<ControllerDevice> controllers,
        IReadOnlyDictionary<string, ControllerProfile> profiles)
    {
        var current = controllers.ToDictionary(ControllerProfileService.DeviceKey,
            StringComparer.OrdinalIgnoreCase);
        if (_previous is null)
        {
            _previous = current;
            return [];
        }

        var changes = new List<ControllerConnectionChange>();
        AddChanges(changes, current.Where(pair => !_previous.ContainsKey(pair.Key))
            .Select(pair => pair.Value), true, current, profiles);
        AddChanges(changes, _previous.Where(pair => !current.ContainsKey(pair.Key))
            .Select(pair => pair.Value), false, _previous, profiles);
        _previous = current;
        return changes;
    }

    private static void AddChanges(List<ControllerConnectionChange> changes,
        IEnumerable<ControllerDevice> transitioned, bool connected,
        IReadOnlyDictionary<string, ControllerDevice> snapshot,
        IReadOnlyDictionary<string, ControllerProfile> profiles)
    {
        foreach (var controller in transitioned)
        {
            var representative = FindGroupMain(controller, snapshot, profiles);
            var key = ControllerProfileService.DeviceKey(representative);
            if (changes.Any(change => change.IsConnected == connected &&
                ControllerProfileService.DeviceKey(change.Controller).Equals(
                    key, StringComparison.OrdinalIgnoreCase)))
                continue;
            changes.Add(new ControllerConnectionChange(representative, connected));
        }
    }

    private static ControllerDevice FindGroupMain(ControllerDevice controller,
        IReadOnlyDictionary<string, ControllerDevice> snapshot,
        IReadOnlyDictionary<string, ControllerProfile> profiles)
    {
        var current = controller;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(ControllerProfileService.DeviceKey(current)) &&
            profiles.TryGetValue(ControllerProfileService.DeviceKey(current), out var profile) &&
            profile.ParentDeviceKey is { Length: > 0 } parentKey &&
            snapshot.TryGetValue(parentKey, out var parent))
        {
            current = parent;
        }
        return current;
    }
}
