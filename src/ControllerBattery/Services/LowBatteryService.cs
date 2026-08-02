using ControllerBattery.Models;

namespace ControllerBattery.Services;

public sealed class LowBatteryService
{
    private readonly HashSet<string> _currentlyLow = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ControllerDevice> FindNewlyLow(IEnumerable<ControllerDevice> controllers)
    {
        var low = controllers.Where(device => !device.IsCharging &&
            device.BatteryLevel is BatteryLevel.Empty or BatteryLevel.Low)
            .ToDictionary(ControllerProfileService.DeviceKey, StringComparer.OrdinalIgnoreCase);
        var transitions = low.Where(pair => !_currentlyLow.Contains(pair.Key))
            .Select(pair => pair.Value).ToArray();
        _currentlyLow.Clear();
        _currentlyLow.UnionWith(low.Keys);
        return transitions;
    }
}
