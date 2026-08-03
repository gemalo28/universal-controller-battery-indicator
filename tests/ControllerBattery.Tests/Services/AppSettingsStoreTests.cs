using System.Windows.Input;
using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery.Tests.Services;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsAndUsesAtomicBackup()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var first = new AppSettings(ModifierKeys.Alt, Key.F8, 20);
        AppSettingsStore.SaveTo(path, first);
        AppSettingsStore.SaveTo(path, first with { PollingIntervalSeconds = 40 });
        Assert.Equal(40, AppSettingsStore.LoadFrom(path).PollingIntervalSeconds);
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void Load_RecoversBackupFromCorruptPrimary()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        AppSettingsStore.SaveTo(path, AppSettings.Default with { PollingIntervalSeconds = 20 });
        AppSettingsStore.SaveTo(path, AppSettings.Default with { PollingIntervalSeconds = 40 });
        File.WriteAllText(path, "corrupt");
        Assert.Equal(20, AppSettingsStore.LoadFrom(path).PollingIntervalSeconds);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsStartWithWindows()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        AppSettingsStore.SaveTo(path, AppSettings.Default with { StartWithWindows = true });

        Assert.True(AppSettingsStore.LoadFrom(path).StartWithWindows);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsConnectionNotifications()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        AppSettingsStore.SaveTo(path, AppSettings.Default with
        {
            ShowConnectionNotifications = false
        });

        Assert.False(AppSettingsStore.LoadFrom(path).ShowConnectionNotifications);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsLowBatteryNotifications()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        AppSettingsStore.SaveTo(path, AppSettings.Default with
        {
            ShowLowBatteryNotifications = false
        });

        Assert.False(AppSettingsStore.LoadFrom(path).ShowLowBatteryNotifications);
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        $"ControllerBattery.Tests.{Guid.NewGuid():N}");
    internal TemporaryDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, true);
}
