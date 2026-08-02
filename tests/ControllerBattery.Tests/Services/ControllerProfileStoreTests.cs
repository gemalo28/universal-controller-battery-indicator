using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery.Tests.Services;

public sealed class ControllerProfileStoreTests
{
    [Fact]
    public void SaveAndLoad_NormalizesProfilesAndRecoversBackup()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "profiles.json");
        var first = new Dictionary<string, ControllerProfile>
            { ["one"] = new("one", " Pad ", "#aabbcc") };
        ControllerProfileStore.SaveTo(path, first);
        ControllerProfileStore.SaveTo(path, new Dictionary<string, ControllerProfile>
            { ["two"] = new("two", null, "#112233") });
        File.WriteAllText(path, "corrupt");
        var recovered = ControllerProfileStore.LoadFrom(path);
        Assert.Equal("Pad", recovered["one"].CustomName);
        Assert.Equal("#AABBCC", recovered["one"].AccentColor);
    }
}
