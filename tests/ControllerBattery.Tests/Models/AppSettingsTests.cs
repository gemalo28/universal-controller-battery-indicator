using System.Windows.Input;
using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery.Tests.Models;

public sealed class AppSettingsTests
{
    [Fact]
    public void Normalize_MigratesLegacyShortcutAndInvalidInterval()
    {
        var value = new AppSettings(ModifierKeys.Control | ModifierKeys.Shift, Key.B, 2);
        var normalized = AppSettingsService.Normalize(value);
        Assert.Equal(AppSettings.Default.OverlayModifiers, normalized.OverlayModifiers);
        Assert.Equal(AppSettings.Default.OverlayKey, normalized.OverlayKey);
        Assert.Equal(AppSettings.DefaultPollingIntervalSeconds, normalized.PollingIntervalSeconds);
    }

    [Fact]
    public void FormatShortcut_UsesSelectedModifiers() =>
        Assert.Equal("Ctrl+Alt+B", AppSettings.FormatShortcut(
            ModifierKeys.Control | ModifierKeys.Alt, Key.B));
}
