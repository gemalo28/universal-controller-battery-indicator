namespace ControllerBattery.Models;

public enum BatteryLevel
{
    Unknown,
    Empty,
    Low,
    Medium,
    High,
    Full
}

public static class BatteryLevelClassifier
{
    public static BatteryLevel FromPercentage(int? percentage) => percentage switch
    {
        null => BatteryLevel.Unknown,
        <= 5 => BatteryLevel.Empty,
        <= 25 => BatteryLevel.Low,
        <= 70 => BatteryLevel.Medium,
        < 100 => BatteryLevel.High,
        _ => BatteryLevel.Full
    };
}
