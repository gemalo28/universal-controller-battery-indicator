using System.Windows.Input;
using ControllerBattery.Models;

namespace ControllerBattery.Services;

public static class AppSettingsService
{
    public static AppSettings Normalize(AppSettings settings)
    {
        if (settings.OverlayModifiers == (ModifierKeys.Control | ModifierKeys.Shift) &&
            settings.OverlayKey == Key.B)
            settings = settings with
            {
                OverlayModifiers = AppSettings.Default.OverlayModifiers,
                OverlayKey = AppSettings.Default.OverlayKey
            };
        return settings.PollingIntervalSeconds is >= AppSettings.MinimumPollingIntervalSeconds
            and <= AppSettings.MaximumPollingIntervalSeconds
            ? settings
            : settings with { PollingIntervalSeconds = AppSettings.DefaultPollingIntervalSeconds };
    }
}
