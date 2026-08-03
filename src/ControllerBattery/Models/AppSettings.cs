using System.Windows.Input;

namespace ControllerBattery.Models;

public enum OverlayPosition
{
    BottomRight,
    BottomLeft,
    TopRight,
    TopLeft
}

public sealed record AppSettings(
    ModifierKeys OverlayModifiers,
    Key OverlayKey,
    int PollingIntervalSeconds = 30,
    OverlayPosition OverlayPosition = OverlayPosition.BottomRight,
    bool StartWithWindows = false,
    bool ShowConnectionNotifications = true,
    bool ShowLowBatteryNotifications = true)
{
    public const int DefaultPollingIntervalSeconds = 30;
    public const int MinimumPollingIntervalSeconds = 5;
    public const int MaximumPollingIntervalSeconds = 300;

    public static AppSettings Default { get; } = new(
        ModifierKeys.Control | ModifierKeys.Alt,
        Key.B,
        DefaultPollingIntervalSeconds);

    public string OverlayShortcutText => FormatShortcut(OverlayModifiers, OverlayKey);

    public static string FormatShortcut(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
